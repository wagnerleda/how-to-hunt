"""
Completes The Wildeast (Trustha Studio; letters and digits only) with punctuation drawn from its own strokes, so
titles, buttons and signs can use it. The dot of the "i", the stem of the "l" and the curve of the "C" are copied,
scaled, sheared or turned into . , : ; ' " - / ( ) ! ? % + and an ellipsis; the chalky texture comes along.

    python tools/fonts/complete_wildeast.py <The Wildeast.ttf> Assets/HowToHunt/Art/Fonts/TheWildeast/TheWildeast-HowToHunt.ttf

Needs fontTools (pip install fonttools). docs/ASSETS.md: the font's license is personal use until bought.
"""
import math
import sys

from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.recordingPen import RecordingPen
from fontTools.pens.reverseContourPen import ReverseContourPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont


def contours(font, name):
    """The glyph's contours as separate recordings."""
    rec = RecordingPen()
    font.getGlyphSet()[name].draw(rec)
    out, cur = [], []
    for op, args in rec.value:
        cur.append((op, args))
        if op in ("closePath", "endPath"):
            out.append(cur)
            cur = []
    return out


def bounds(parts):
    pen = BoundsPen(None)
    for c in parts:
        for op, args in c:
            getattr(pen, op)(*args)
    return pen.bounds


def replay(parts, pen):
    for c in parts:
        for op, args in c:
            getattr(pen, op)(*args)


class Shape:
    """Some contours placed with an affine transform (a, b, c, d, dx, dy)."""

    def __init__(self, parts, matrix, mirrored=False):
        self.parts, self.matrix, self.mirrored = parts, matrix, mirrored


def place(parts, width=None, height=None, x=0.0, y=0.0, shear=0.0, rotate=0.0, mirror=False):
    """Scale parts to width/height (before turning; one may be None: keep proportions), turn, shear, then put their
    min corner at x, y."""
    x0, y0, x1, y1 = bounds(parts)
    w, h = x1 - x0, y1 - y0
    sx = width / w if width else (height / h if height else 1.0)
    sy = height / h if height else sx
    if mirror:
        sx = -sx
    r = math.radians(rotate)
    cos, sin = math.cos(r), math.sin(r)
    # local: move to origin, scale, rotate, shear
    a, b, c, d = sx * cos, sx * sin, -sy * sin, sy * cos
    a, c = a + shear * b, c + shear * d
    # bounds after the linear part, to put the min corner at x, y
    corners = [(px - x0, py - y0) for px in (x0, x1) for py in (y0, y1)]
    tx = [a * px + c * py for px, py in corners]
    ty = [b * px + d * py for px, py in corners]
    dx, dy = x - min(tx) - a * x0 - c * y0, y - min(ty) - b * x0 - d * y0
    return Shape(parts, (a, b, c, d, dx, dy), (a * d - b * c) < 0)


def build(font, shapes):
    pen = TTGlyphPen(font.getGlyphSet())
    for s in shapes:
        tp = TransformPen(pen, s.matrix)
        replay(s.parts, ReverseContourPen(tp) if s.mirrored else tp)
    return pen.glyph()


def main(src, dst):
    font = TTFont(src)
    cmap = font.getBestCmap()
    i_parts = contours(font, cmap[ord("i")])
    _, _, _, top = bounds(i_parts)
    # The dot of the "i": the contours entirely above 70 % of its height.
    dot = [c for c in i_parts if bounds([c])[1] > top * 0.62]
    stem = contours(font, cmap[ord("l")])
    curve = contours(font, cmap[ord("C")])
    ring = contours(font, cmap[ord("o")])
    cap = 600.0
    dw = bounds(dot)[2] - bounds(dot)[0]
    d = 120.0  # dot size

    glyphs = {
        ".": ([place(dot, height=d, x=30, y=0)], 200),
        ",": ([place(dot, height=d, x=30, y=0), place(stem, width=45, height=120, x=40, y=-110, shear=0.45)], 200),
        ":": ([place(dot, height=d, x=30, y=0), place(dot, height=d, x=30, y=300)], 200),
        ";": ([place(dot, height=d, x=30, y=300), place(dot, height=d, x=30, y=0), place(stem, width=45, height=120, x=40, y=-110, shear=0.45)], 200),
        "'": ([place(stem, width=60, height=210, x=40, y=430)], 150),
        "’": ([place(stem, width=60, height=210, x=40, y=430, shear=0.2)], 150),
        "\"": ([place(stem, width=60, height=210, x=40, y=430), place(stem, width=60, height=210, x=140, y=430)], 260),
        "-": ([place(stem, width=75, height=260, x=30, y=230, rotate=90)], 320),
        "–": ([place(stem, width=75, height=360, x=30, y=230, rotate=90)], 420),
        "—": ([place(stem, width=75, height=560, x=30, y=230, rotate=90)], 620),
        "/": ([place(stem, width=90, height=640, x=30, y=-40, shear=0.42)], 380),
        "(": ([place(curve, width=150, height=720, x=40, y=-80)], 220),
        ")": ([place(curve, width=150, height=720, x=30, y=-80, mirror=True)], 220),
        "!": ([place(stem, width=110, height=470, x=40, y=170), place(dot, height=d, x=45, y=0)], 220),
        "?": ([place(curve, width=240, height=270, x=20, y=350, rotate=-90), place(stem, width=90, height=170, x=150, y=170), place(dot, height=d, x=140, y=0)], 330),
        "+": ([place(stem, width=90, height=340, x=135, y=80), place(stem, width=90, height=340, x=10, y=205, rotate=90)], 380),
        "%": ([place(ring, height=200, x=20, y=380), place(stem, width=70, height=640, x=120, y=-40, shear=0.42), place(ring, height=200, x=290, y=0)], 520),
        "…": ([place(dot, height=d, x=30, y=0), place(dot, height=d, x=170, y=0), place(dot, height=d, x=310, y=0)], 460),
        "#": ([place(stem, width=70, height=520, x=100, y=0, shear=0.15), place(stem, width=70, height=520, x=250, y=0, shear=0.15),
               place(stem, width=70, height=380, x=40, y=150, rotate=90), place(stem, width=70, height=380, x=60, y=330, rotate=90)], 480),
    }

    glyf, hmtx = font["glyf"], font["hmtx"]
    order = font.getGlyphOrder()
    for ch, (shapes, adv) in glyphs.items():
        name = "uni%04X" % ord(ch)
        glyf[name] = build(font, shapes)
        g = glyf[name]
        g.recalcBounds(glyf)
        hmtx[name] = (adv, max(0, g.xMin) if g.numberOfContours else 0)
        if name not in order:
            order.append(name)
        for table in font["cmap"].tables:
            if table.isUnicode():
                table.cmap[ord(ch)] = name
    font.setGlyphOrder(order)
    font["maxp"].numGlyphs = len(order)
    # No hinting instructions for the new glyphs; drop the hdmx/LTSH/VDMX device tables they'd invalidate.
    for t in ("hdmx", "LTSH", "VDMX"):
        if t in font:
            del font[t]
    font["name"].setName("The Wildeast HowToHunt", 4, 3, 1, 0x409)
    font.save(dst)
    print(f"wrote {dst} with {len(glyphs)} glyphs added")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
