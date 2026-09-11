#!/usr/bin/env python3
"""Rasterise the LANCommander wordmark into assets/logo.png.

The Avalonia launcher puts `<svg:Svg Path="/Assets/logo.svg" Width="350"/>` at
the top of its login and server-select cards. This launcher has no SVG
renderer — the whole graphics stack is a software blitter over stb — so the
mark ships as a PNG and is decoded to whatever width the card gives it, the
same arrangement the icons use.

Source is LANCommander.Launcher/Assets/logo.svg, so the two launchers cannot
drift.

The file is simple enough to rasterise without a general SVG implementation:
nineteen elements, all `fill-rule:nonzero`, no transforms, no gradients, no
strokes and no opacity. Two of them are the white slabs and the rest are the
black artwork knocked out of them, so the only thing this has to get right
beyond a scanline fill is compositing them in document order.

Antialiasing is supersampled in y and analytic in x: each scanline span
contributes its exact fractional coverage to the pixels at either end. The
mark is mostly steep diagonals, where x coverage is what the eye reads, and it
makes the edges cleaner than a box downsample at the same cost.

Usage (from LANCommander.Launcher.Legacy/):
    python tools/generate-logo.py
"""

import os
import re
import sys

import numpy as np
from PIL import Image

from svgpath import parse_path

# Stored width. The cards ask for something under 300, so this only ever
# downscales, which is what the runtime's Mitchell filter is good at.
LOGO_WIDTH_PX = 512

# Scanline samples per output row.
SS = 4

# SVG's default fill when an element does not name one.
DEFAULT_FILL = (0, 0, 0)


def parse_fill(style):
    """The fill colour an element's style declares, or SVG's default black."""
    if not style:
        return DEFAULT_FILL

    m = re.search(r"fill\s*:\s*#([0-9a-fA-F]{3,6})", style)
    if not m:
        return DEFAULT_FILL

    h = m.group(1)
    if len(h) == 3:
        h = "".join(c * 2 for c in h)
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16))


def rect_to_subpaths(attrs):
    """A <rect> as the single closed subpath parse_path would have produced."""
    def num(name, default=0.0):
        m = re.search(r'\b%s="([-0-9.eE]+)"' % name, attrs)
        return float(m.group(1)) if m else default

    if re.search(r'\br[xy]="', attrs):
        raise ValueError("rounded <rect> is not supported")

    x, y = num("x"), num("y")
    w, h = num("width"), num("height")
    if w <= 0 or h <= 0:
        return []

    return [([(x, y), (x + w, y), (x + w, y + h), (x, y + h)], True)]


def read_svg(path):
    """(viewBox width, height, [(subpaths, rgb)]) in document order."""
    with open(path, "r", encoding="utf-8") as f:
        text = f.read()

    m = re.search(r'viewBox="([-0-9.eE]+)\s+([-0-9.eE]+)\s+'
                  r'([-0-9.eE]+)\s+([-0-9.eE]+)"', text)
    if not m:
        raise ValueError("no viewBox")

    vb_x, vb_y, vb_w, vb_h = (float(g) for g in m.groups())

    elements = []
    for tag, attrs in re.findall(r"<(path|rect)\b([^>]*)>", text):
        style = re.search(r'style="([^"]*)"', attrs)
        fill = parse_fill(style.group(1) if style else None)

        if tag == "rect":
            subpaths = rect_to_subpaths(attrs)
        else:
            d = re.search(r'\sd="([^"]*)"', attrs)
            if not d:
                continue
            subpaths = parse_path(d.group(1))

        if subpaths:
            elements.append((subpaths, fill))

    # Shift the viewBox origin to 0,0 so the rasteriser only deals in scale.
    if vb_x or vb_y:
        elements = [([([(x - vb_x, y - vb_y) for (x, y) in pts], closed)
                      for (pts, closed) in subpaths], fill)
                    for (subpaths, fill) in elements]

    return vb_w, vb_h, elements


def fill_coverage(subpaths, w, h, scale):
    """Nonzero-winding fill of `subpaths` -> float coverage in [0, 1], h x w."""
    edges = []
    for points, _closed in subpaths:
        n = len(points)
        for k in range(n):
            x0, y0 = points[k]
            x1, y1 = points[(k + 1) % n]   # a fill always closes
            if y0 == y1:
                continue
            edges.append((x0 * scale, y0 * scale, x1 * scale, y1 * scale))

    cov = np.zeros((h, w), dtype=np.float64)
    if not edges:
        return cov

    e = np.asarray(edges, dtype=np.float64)
    ex0, ey0, ex1, ey1 = e[:, 0], e[:, 1], e[:, 2], e[:, 3]
    direction = np.where(ey1 > ey0, 1, -1)
    y_lo = np.minimum(ey0, ey1)
    y_hi = np.maximum(ey0, ey1)

    # Only the rows the shape actually touches.
    row_first = max(0, int(np.floor(y_lo.min())))
    row_last = min(h - 1, int(np.ceil(y_hi.max())))

    acc = np.empty(w + 1, dtype=np.float64)

    for py in range(row_first, row_last + 1):
        acc[:] = 0.0

        for s in range(SS):
            sy = py + (s + 0.5) / SS

            hit = (y_lo <= sy) & (sy < y_hi)
            if not hit.any():
                continue

            t = (sy - ey0[hit]) / (ey1[hit] - ey0[hit])
            xs = ex0[hit] + t * (ex1[hit] - ex0[hit])
            ds = direction[hit]

            order = np.argsort(xs, kind="stable")
            xs = xs[order]
            winding = np.cumsum(ds[order])

            # A span runs from each crossing that leaves winding non-zero to
            # the next crossing that brings it back to zero.
            inside = winding != 0
            starts = xs[:-1][inside[:-1]]
            ends = xs[1:][inside[:-1]]

            for xa, xb in zip(starts, ends):
                if xb <= 0.0 or xa >= w or xb <= xa:
                    continue
                xa = max(xa, 0.0)
                xb = min(xb, float(w))

                ia = int(xa)
                ib = int(xb)

                if ia == ib:
                    acc[ia] += xb - xa
                else:
                    acc[ia] += (ia + 1) - xa
                    if ib > ia + 1:
                        acc[ia + 1:ib] += 1.0
                    if ib < w:
                        acc[ib] += xb - ib

        cov[py] = np.minimum(acc[:w] / SS, 1.0)

    return cov


def composite(dst_rgb, dst_a, cov, colour):
    """Source-over `colour` at `cov` alpha onto straight-alpha RGBA planes."""
    src_a = cov
    out_a = src_a + dst_a * (1.0 - src_a)

    safe = np.where(out_a > 0.0, out_a, 1.0)
    for c in range(3):
        dst_rgb[..., c] = (colour[c] * src_a +
                           dst_rgb[..., c] * dst_a * (1.0 - src_a)) / safe

    return out_a


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    launcher_dir = os.path.dirname(here)
    repo_root = os.path.dirname(launcher_dir)

    src = os.path.join(repo_root, "LANCommander.Launcher", "Assets", "logo.svg")
    if not os.path.exists(src):
        print("Could not find %s" % src, file=sys.stderr)
        return 1

    vb_w, vb_h, elements = read_svg(src)

    scale = LOGO_WIDTH_PX / vb_w
    w = LOGO_WIDTH_PX
    h = int(round(vb_h * scale))

    rgb = np.zeros((h, w, 3), dtype=np.float64)
    alpha = np.zeros((h, w), dtype=np.float64)

    for subpaths, colour in elements:
        cov = fill_coverage(subpaths, w, h, scale)
        alpha = composite(rgb, alpha, cov, colour)

    rgba = np.empty((h, w, 4), dtype=np.uint8)
    rgba[..., 0:3] = np.clip(rgb + 0.5, 0, 255).astype(np.uint8)
    rgba[..., 3] = np.clip(alpha * 255.0 + 0.5, 0, 255).astype(np.uint8)

    dest = os.path.join(launcher_dir, "assets", "logo.png")
    Image.fromarray(rgba, "RGBA").save(dest, optimize=True)
    print("logo.svg (%gx%g) -> assets/logo.png (%dx%d), %d elements"
          % (vb_w, vb_h, w, h, len(elements)))

    return 0


if __name__ == "__main__":
    sys.exit(main())
