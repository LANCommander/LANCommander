#!/usr/bin/env python3
"""SVG path data -> flattened polylines.

Shared by the two rasterisers in this directory: generate-icons.py, which
strokes Phosphor's centreline geometry, and generate-logo.py, which fills the
brand mark. Neither needs a full SVG implementation — no transforms, no
gradients, no clip paths — but both need the same path grammar, and having one
copy of the arc conversion is worth a module on its own.

CURVE_STEPS is the flattening resolution. At the sizes these are rasterised
for it is well under half a pixel of error.
"""

import math
import re

CURVE_STEPS = 24


# ---------------------------------------------------------------------------
# Path parsing
# ---------------------------------------------------------------------------

TOKEN_RE = re.compile(r"[MmLlHhVvCcSsQqTtAaZz]|-?\d*\.?\d+(?:[eE][-+]?\d+)?")


def tokenize(d):
    return TOKEN_RE.findall(d)


def cubic(p0, p1, p2, p3, out):
    """Flatten one cubic bezier, excluding the start point."""
    for i in range(1, CURVE_STEPS + 1):
        t = i / CURVE_STEPS
        mt = 1.0 - t
        a = mt * mt * mt
        b = 3.0 * mt * mt * t
        c = 3.0 * mt * t * t
        e = t * t * t
        out.append((a * p0[0] + b * p1[0] + c * p2[0] + e * p3[0],
                    a * p0[1] + b * p1[1] + c * p2[1] + e * p3[1]))


def arc_to_cubics(p0, rx, ry, phi_deg, large_arc, sweep, p1, out):
    """SVG endpoint arc -> a series of cubics, per the F.6 implementation notes."""
    if rx == 0 or ry == 0 or (p0[0] == p1[0] and p0[1] == p1[1]):
        out.append(p1)
        return

    rx, ry = abs(rx), abs(ry)
    phi = math.radians(phi_deg)
    cos_p, sin_p = math.cos(phi), math.sin(phi)

    dx2 = (p0[0] - p1[0]) / 2.0
    dy2 = (p0[1] - p1[1]) / 2.0
    x1p = cos_p * dx2 + sin_p * dy2
    y1p = -sin_p * dx2 + cos_p * dy2

    # Scale the radii up if they are too small to span the endpoints.
    lam = (x1p * x1p) / (rx * rx) + (y1p * y1p) / (ry * ry)
    if lam > 1.0:
        s = math.sqrt(lam)
        rx *= s
        ry *= s

    num = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p
    den = rx * rx * y1p * y1p + ry * ry * x1p * x1p
    coef = math.sqrt(max(0.0, num / den)) if den else 0.0
    if large_arc == sweep:
        coef = -coef

    cxp = coef * rx * y1p / ry
    cyp = -coef * ry * x1p / rx

    cx = cos_p * cxp - sin_p * cyp + (p0[0] + p1[0]) / 2.0
    cy = sin_p * cxp + cos_p * cyp + (p0[1] + p1[1]) / 2.0

    def angle(ux, uy, vx, vy):
        dot = ux * vx + uy * vy
        n = math.hypot(ux, uy) * math.hypot(vx, vy)
        if n == 0:
            return 0.0
        a = math.acos(max(-1.0, min(1.0, dot / n)))
        return -a if (ux * vy - uy * vx) < 0 else a

    theta1 = angle(1.0, 0.0, (x1p - cxp) / rx, (y1p - cyp) / ry)
    dtheta = angle((x1p - cxp) / rx, (y1p - cyp) / ry,
                   (-x1p - cxp) / rx, (-y1p - cyp) / ry)

    if not sweep and dtheta > 0:
        dtheta -= 2.0 * math.pi
    elif sweep and dtheta < 0:
        dtheta += 2.0 * math.pi

    # Split into <= 90 degree pieces; a cubic approximates no more than that
    # to within a usable tolerance.
    segments = max(1, int(math.ceil(abs(dtheta) / (math.pi / 2.0))))
    delta = dtheta / segments
    t = (4.0 / 3.0) * math.tan(delta / 4.0)

    start = theta1
    cur = p0
    for _ in range(segments):
        end = start + delta

        cos1, sin1 = math.cos(start), math.sin(start)
        cos2, sin2 = math.cos(end), math.sin(end)

        def point(c, s):
            return (cx + rx * c * cos_p - ry * s * sin_p,
                    cy + rx * c * sin_p + ry * s * cos_p)

        def deriv(c, s):
            return (-rx * s * cos_p - ry * c * sin_p,
                    -rx * s * sin_p + ry * c * cos_p)

        e1 = point(cos1, sin1)
        e2 = point(cos2, sin2)
        d1 = deriv(cos1, sin1)
        d2 = deriv(cos2, sin2)

        c1 = (e1[0] + t * d1[0], e1[1] + t * d1[1])
        c2 = (e2[0] - t * d2[0], e2[1] - t * d2[1])

        cubic(cur, c1, c2, e2, out)
        cur = e2
        start = end


def parse_path(d):
    """Return a list of (points, closed) subpaths, each points a list of (x, y).

    `closed` matters for stroking in a way it did not for filling: an open
    subpath gets a round cap at each end, a closed one gets a round join
    across the seam. Phosphor's regular weight is full of both -- Storefront
    is a closed awning over four open scallops.
    """
    tokens = tokenize(d)
    i = 0
    subpaths = []
    cur = []
    pos = (0.0, 0.0)
    start = (0.0, 0.0)
    cmd = None
    prev_cubic_ctrl = None
    prev_quad_ctrl = None

    def num():
        nonlocal i
        v = float(tokens[i])
        i += 1
        return v

    def flush(closed=False):
        nonlocal cur
        if len(cur) >= 2:
            subpaths.append((cur, closed))
        cur = []

    while i < len(tokens):
        tok = tokens[i]
        if re.match(r"[A-Za-z]", tok):
            cmd = tok
            i += 1
            if cmd in "Zz":
                flush(closed=True)
                pos = start
                prev_cubic_ctrl = prev_quad_ctrl = None
                continue
        elif cmd is None:
            break
        else:
            # Repeated coordinate set: M becomes L, m becomes l.
            if cmd == "M":
                cmd = "L"
            elif cmd == "m":
                cmd = "l"

        rel = cmd.islower()
        c = cmd.upper()

        if c == "M":
            x, y = num(), num()
            if rel:
                x, y = pos[0] + x, pos[1] + y
            flush()
            pos = start = (x, y)
            cur = [pos]
            prev_cubic_ctrl = prev_quad_ctrl = None

        elif c == "L":
            x, y = num(), num()
            if rel:
                x, y = pos[0] + x, pos[1] + y
            pos = (x, y)
            cur.append(pos)
            prev_cubic_ctrl = prev_quad_ctrl = None

        elif c == "H":
            x = num()
            if rel:
                x = pos[0] + x
            pos = (x, pos[1])
            cur.append(pos)
            prev_cubic_ctrl = prev_quad_ctrl = None

        elif c == "V":
            y = num()
            if rel:
                y = pos[1] + y
            pos = (pos[0], y)
            cur.append(pos)
            prev_cubic_ctrl = prev_quad_ctrl = None

        elif c in ("C", "S"):
            if c == "C":
                x1, y1 = num(), num()
                if rel:
                    x1, y1 = pos[0] + x1, pos[1] + y1
            else:
                # Smooth: reflect the previous control point.
                if prev_cubic_ctrl:
                    x1 = 2 * pos[0] - prev_cubic_ctrl[0]
                    y1 = 2 * pos[1] - prev_cubic_ctrl[1]
                else:
                    x1, y1 = pos
            x2, y2 = num(), num()
            x3, y3 = num(), num()
            if rel:
                x2, y2 = pos[0] + x2, pos[1] + y2
                x3, y3 = pos[0] + x3, pos[1] + y3
            if not cur:
                cur = [pos]
            cubic(pos, (x1, y1), (x2, y2), (x3, y3), cur)
            prev_cubic_ctrl = (x2, y2)
            prev_quad_ctrl = None
            pos = (x3, y3)

        elif c in ("Q", "T"):
            if c == "Q":
                qx, qy = num(), num()
                if rel:
                    qx, qy = pos[0] + qx, pos[1] + qy
            else:
                if prev_quad_ctrl:
                    qx = 2 * pos[0] - prev_quad_ctrl[0]
                    qy = 2 * pos[1] - prev_quad_ctrl[1]
                else:
                    qx, qy = pos
            x2, y2 = num(), num()
            if rel:
                x2, y2 = pos[0] + x2, pos[1] + y2
            # Quadratic -> cubic.
            c1 = (pos[0] + 2.0 / 3.0 * (qx - pos[0]), pos[1] + 2.0 / 3.0 * (qy - pos[1]))
            c2 = (x2 + 2.0 / 3.0 * (qx - x2), y2 + 2.0 / 3.0 * (qy - y2))
            if not cur:
                cur = [pos]
            cubic(pos, c1, c2, (x2, y2), cur)
            prev_quad_ctrl = (qx, qy)
            prev_cubic_ctrl = None
            pos = (x2, y2)

        elif c == "A":
            rx, ry = num(), num()
            rot = num()
            large = int(num())
            sweep = int(num())
            x, y = num(), num()
            if rel:
                x, y = pos[0] + x, pos[1] + y
            if not cur:
                cur = [pos]
            arc_to_cubics(pos, rx, ry, rot, large, sweep, (x, y), cur)
            prev_cubic_ctrl = prev_quad_ctrl = None
            pos = (x, y)

        else:
            raise ValueError("unsupported path command %r" % cmd)

    flush()
    return subpaths
