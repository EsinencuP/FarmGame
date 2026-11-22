"""Low-poly SVG helpers: faceted polygons shaded from a top-left light."""
import math

LIGHT = (-0.55, -0.83)

# Palette taken from the game world (grass, trees, lanterns, water, sky)
P = {
    "grass": "#5FB236", "grass_dk": "#3E8A27", "leaf": "#8FD14F", "leaf_dk": "#5E9E2E",
    "wood": "#A0673A", "wood_dk": "#6E4423", "wood_lt": "#C98B55",
    "soil": "#8A5A3B", "stone": "#9AA0A6", "stone_dk": "#6D7378",
    "gold": "#F5C542", "gold_dk": "#D9962B", "orange": "#F29B38", "red": "#E2533F",
    "water": "#2F9FA8", "water_dk": "#1E6F78", "sky": "#6EC1F0", "sky_dk": "#3E8FD0",
    "cream": "#FFF4DC", "cream_dk": "#F0DDB4", "ink": "#3B2A1A", "white": "#FFFFFF",
    "purple": "#8E6CC9", "pink": "#F08BA8", "yellow": "#FFE066",
}


def _rgb(h):
    h = h.lstrip("#")
    return [int(h[i:i + 2], 16) for i in (0, 2, 4)]


def shade(h, t):
    """t>0 lightens towards white, t<0 darkens towards black."""
    r = _rgb(h)
    if t >= 0:
        r = [c + (255 - c) * t for c in r]
    else:
        r = [c * (1 + t) for c in r]
    return "#%02X%02X%02X" % tuple(max(0, min(255, round(c))) for c in r)


def _pts(points):
    return " ".join(f"{x:.2f},{y:.2f}" for x, y in points)


def flat(points, color, extra=""):
    return f'<polygon points="{_pts(points)}" fill="{color}" stroke="{color}" stroke-width="0.6" stroke-linejoin="round"{extra}/>'


def facet(points, color, center=None, strength=0.26, seed=0):
    """Fan-triangulate a star-shaped polygon from its center and shade each facet
    by the direction of its outer edge relative to the light — gives the gem-like
    low-poly look of the in-game trees and rocks."""
    if center is None:
        center = (sum(p[0] for p in points) / len(points), sum(p[1] for p in points) / len(points))
    cx, cy = center
    out = []
    n = len(points)
    for i in range(n):
        a, b = points[i], points[(i + 1) % n]
        mx, my = (a[0] + b[0]) / 2 - cx, (a[1] + b[1]) / 2 - cy
        d = math.hypot(mx, my) or 1
        t = (mx / d) * LIGHT[0] + (my / d) * LIGHT[1]
        jitter = ((i * 37 + seed * 11) % 7 - 3) * 0.012
        out.append(flat([center, a, b], shade(color, t * strength + jitter)))
    return "".join(out)


def ngon(cx, cy, r, n, rot=0.0, sx=1.0, sy=1.0, wobble=0.0, seed=1):
    pts = []
    for i in range(n):
        a = rot + 2 * math.pi * i / n - math.pi / 2
        rr = r * (1 + wobble * (((i * 53 + seed * 17) % 9) / 8 - 0.5))
        pts.append((cx + rr * math.cos(a) * sx, cy + rr * math.sin(a) * sy))
    return pts


def blob(cx, cy, r, n=9, seed=1, sx=1.0, sy=1.0, color=P["leaf"], wobble=0.18, center=None):
    pts = ngon(cx, cy, r, n, rot=seed * 0.3, sx=sx, sy=sy, wobble=wobble, seed=seed)
    c = center or (cx - r * 0.18 * sx, cy - r * 0.22 * sy)
    return facet(pts, color, center=c, seed=seed)


def stick(x1, y1, x2, y2, w, color):
    """A board/handle split lengthwise into a lit and a shaded half."""
    dx, dy = x2 - x1, y2 - y1
    L = math.hypot(dx, dy) or 1
    nx, ny = -dy / L * w / 2, dx / L * w / 2
    a, b = (x1 + nx, y1 + ny), (x2 + nx, y2 + ny)
    c, d = (x2 - nx, y2 - ny), (x1 - nx, y1 - ny)
    m1, m2 = (x1, y1), (x2, y2)
    lit_first = (nx * LIGHT[0] + ny * LIGHT[1]) > 0
    s1, s2 = (shade(color, 0.14), shade(color, -0.16)) if lit_first else (shade(color, -0.16), shade(color, 0.14))
    return flat([a, b, m2, m1], s1) + flat([m1, m2, c, d], s2)


def rect_pts(x, y, w, h):
    return [(x, y), (x + w, y), (x + w, y + h), (x, y + h)]


def ground_shadow(cx, cy, rx, ry=None):
    ry = ry or rx * 0.28
    return f'<ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}" fill="#000" opacity="0.12"/>'
