#!/usr/bin/env python3
"""Rasterise the icons the legacy launcher draws into assets/icons/.

The legacy launcher has no vector rasteriser and no icon font: it draws through
a software surface blitter, and the one image decoder it has is stb. So icons
ship as PNGs and are tinted at blit time, the same way glyph masks are.

Source is the Phosphor **Regular** weight vendored for the Avalonia launcher,
in LANCommander.Launcher/Assets/Icons/Regular.axaml, and the job here is to
reproduce what Avalonia does with it. Those entries are Phosphor's *centreline*
geometry -- open polylines, not closed shapes -- and App.axaml renders them with

    <Path Fill="Transparent" Stroke="{Foreground}" StrokeThickness="1"
          Stretch="Uniform" />

so what the Avalonia launcher actually shows is a hairline outline, not a solid
glyph. This script used to read Fill.axaml and scanline-fill it, which is why
the legacy launcher's icons read as much heavier than the Avalonia launcher's.

Two consequences for the rasteriser:

  * Stroking, not filling. Each subpath is flattened to a polyline and the
    stroke is rendered as a distance field -- coverage falls off over the last
    pixel of the half-width -- which gives round caps and round joins for free.
    That is exactly Phosphor's stroke-linecap/linejoin, and it antialiases
    better than the supersample-and-box-downsample the fill path used.

  * Uniform stretch, per icon. Avalonia sizes each Path from its own bounds,
    not from the 256 viewbox, so a glyph that does not fill the viewbox is
    scaled up until it does. Matching that is the difference between icons
    that are the same size in both launchers and icons that are ~10% small.

STROKE_UNITS is Phosphor's own regular weight in viewbox units. It is also what
Avalonia ends up drawing: 16/256 of a 16px icon is 1px, which is the literal
StrokeThickness above.

Each icon is written once at ICON_SOURCE_PX. The launcher downscales to
whatever size it needs with the stb Mitchell filter it already uses for cover
art, so there is no size matrix to maintain here.

Output is RGBA: white RGB with coverage in alpha. That is the "mask surface"
shape gfx::blit_tinted() documents, so one file serves every theme colour.

Usage (from LANCommander.Launcher.Legacy/):
    python tools/generate-icons.py
"""

import os
import re
import sys

import numpy as np
from PIL import Image

from svgpath import parse_path

# Phosphor draws on a 256x256 viewbox.
VIEWBOX = 256.0

# Stored size. Downscaled at runtime; large enough that 24px and 32px still
# look clean, small enough that twenty of them are a rounding error on disk.
ICON_SOURCE_PX = 64

# Phosphor's regular stroke width, in viewbox units. Equivalent to the
# StrokeThickness="1" Avalonia renders a 16px icon with.
STROKE_UNITS = 16.0

# The icons the launcher actually draws. Left is the Phosphor name in
# Regular.axaml, right is the file written to assets/icons/.
#
# The pairings follow the Avalonia launcher's call sites, not the file names:
# `download` is the footer's Downloads button, which is Avalonia's
# DownloadSimple (ShellView.axaml), while `install` is the arrow-in-a-circle
# the download queue falls back to for a game with no icon (ArrowCircleDown).
# Those two were the wrong way round.
ICONS = {
    "ArrowsClockwise": "refresh",
    "CaretLeft": "caret-left",
    "CaretRight": "caret-right",
    "CaretDown": "caret-down",
    "CaretUp": "caret-up",
    "ArrowLeft": "arrow-left",
    "Books": "library",
    "Storefront": "depot",
    "DownloadSimple": "download",
    "ArrowCircleDown": "install",
    "Play": "play",
    "Stop": "stop",
    "Plus": "plus",
    "X": "close",
    "Minus": "minimize",
    "Square": "maximize",
    "User": "user",
    "MagnifyingGlass": "search",
    "GearSix": "settings",
    "FolderOpen": "folder",
    "Trash": "trash",
    "Check": "check",
}


# ---------------------------------------------------------------------------
# Rasterisation
# ---------------------------------------------------------------------------

def segments_of(subpaths):
    """Flattened subpaths -> one (N, 4) array of x0, y0, x1, y1 segments."""
    segs = []
    for points, closed in subpaths:
        n = len(points)
        last = n if closed else n - 1
        for k in range(last):
            x0, y0 = points[k]
            x1, y1 = points[(k + 1) % n]
            segs.append((x0, y0, x1, y1))
    return np.asarray(segs, dtype=np.float64).reshape(-1, 4)


def stroke_bounds(segs, half_w):
    """Bounding box of the stroked geometry, in viewbox units."""
    xs = np.concatenate((segs[:, 0], segs[:, 2]))
    ys = np.concatenate((segs[:, 1], segs[:, 3]))
    return (xs.min() - half_w, ys.min() - half_w,
            xs.max() + half_w, ys.max() + half_w)


def rasterise_stroke(subpaths, size, stroke_units=STROKE_UNITS):
    """Stroke `subpaths` into a uint8 coverage mask, `size` x `size`.

    Coverage comes from an unsigned distance field rather than a polygon
    union: a pixel is inside the stroke when its centre is within half the
    stroke width of the polyline, and the last pixel of that radius ramps
    from 1 to 0. Round caps and round joins fall out of the distance metric
    for free, which is what Phosphor's stroke-linecap/linejoin ask for.

    The geometry is scaled to fill `size` the way Avalonia's Stretch="Uniform"
    fills the Icon control: from the STROKED bounds of this glyph, not from
    the 256 viewbox. Inflating by the half width first also removes the
    degenerate case -- Minus is a single horizontal line whose unstroked
    bounds are zero pixels tall.
    """
    mask = np.zeros((size, size), dtype=np.uint8)

    segs = segments_of(subpaths)
    if segs.size == 0:
        return mask

    half_w = stroke_units / 2.0
    x0b, y0b, x1b, y1b = stroke_bounds(segs, half_w)

    span = max(x1b - x0b, y1b - y0b)
    if span <= 0:
        return mask

    scale = size / span
    # Centre the shorter axis inside the square.
    off_x = (size - (x1b - x0b) * scale) / 2.0
    off_y = (size - (y1b - y0b) * scale) / 2.0

    ax = (segs[:, 0] - x0b) * scale + off_x
    ay = (segs[:, 1] - y0b) * scale + off_y
    bx = (segs[:, 2] - x0b) * scale + off_x
    by = (segs[:, 3] - y0b) * scale + off_y

    half_px = half_w * scale

    # Pixel centres.
    grid = np.arange(size, dtype=np.float64) + 0.5
    px = grid[None, :]
    py = grid[:, None]

    dist = np.full((size, size), np.inf)

    for i in range(len(ax)):
        ex = bx[i] - ax[i]
        ey = by[i] - ay[i]
        len_sq = ex * ex + ey * ey

        vx = px - ax[i]
        vy = py - ay[i]

        if len_sq > 0.0:
            t = np.clip((vx * ex + vy * ey) / len_sq, 0.0, 1.0)
            dx = vx - t * ex
            dy = vy - t * ey
        else:
            dx, dy = vx, vy

        np.minimum(dist, np.hypot(dx, dy), out=dist)

    # One pixel of linear falloff at the edge of the stroke.
    coverage = np.clip(half_px + 0.5 - dist, 0.0, 1.0)

    return (coverage * 255.0 + 0.5).astype(np.uint8)


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    launcher_dir = os.path.dirname(here)
    repo_root = os.path.dirname(launcher_dir)

    src = os.path.join(repo_root, "LANCommander.Launcher",
                       "Assets", "Icons", "Regular.axaml")
    if not os.path.exists(src):
        print("Could not find %s" % src, file=sys.stderr)
        return 1

    with open(src, "r", encoding="utf-8") as f:
        text = f.read()

    paths = dict(re.findall(
        r'x:Key="Regular_([A-Za-z0-9]+)"\s*>(.*?)</StreamGeometry>', text, re.S))

    out_dir = os.path.join(launcher_dir, "assets", "icons")
    os.makedirs(out_dir, exist_ok=True)

    missing = [n for n in ICONS if n not in paths]
    if missing:
        print("Not in Regular.axaml: %s" % ", ".join(sorted(missing)),
              file=sys.stderr)
        return 1

    for name, out_name in sorted(ICONS.items()):
        coverage = rasterise_stroke(parse_path(paths[name].strip()),
                                    ICON_SOURCE_PX)

        rgba = np.empty((ICON_SOURCE_PX, ICON_SOURCE_PX, 4), dtype=np.uint8)
        rgba[..., 0:3] = 255       # white; the launcher tints at blit time
        rgba[..., 3] = coverage

        dest = os.path.join(out_dir, out_name + ".png")
        Image.fromarray(rgba, "RGBA").save(dest, optimize=True)
        print("%-16s -> assets/icons/%s.png" % (name, out_name))

    return 0


if __name__ == "__main__":
    sys.exit(main())
