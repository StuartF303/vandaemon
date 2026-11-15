"""Find clear F.SilkS space for board-level text.

Written after a placement that looked clear and produced 11 silk_overlap warnings.
The bug worth remembering: KiCad writes the layer as **"F.SilkS"**, not
"F.Silkscreen" -- a filter matching "Silkscreen" silently matches nothing and every
region reads as empty. The DRC report *displays* "F.Silkscreen", which is what makes
the mistake easy. Occupancy here also includes fp_text (reference designators), which
are silkscreen too and were the other half of the miss.

Usage:  "C:/Program Files/KiCad/10.0/bin/python.exe" tools/silkspace.py W H [layer]
        -> prints candidate centres for a clear W x H mm block
"""
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
BOARD = os.path.join(os.path.dirname(HERE), "VANDIMMER-4CH2A.kicad_pcb")
sys.path.insert(0, HERE)
import geom  # noqa: E402

SILK = '"F.SilkS"'


def _xf(fx, fy, rot):
    a = math.radians(-rot)
    ca, sa = math.cos(a), math.sin(a)
    return lambda px, py: (fx + px * ca - py * sa, fy + px * sa + py * ca)


def silk_boxes(path=BOARD, layer=SILK):
    """Bounding boxes of every silkscreen item, in board coordinates."""
    src = open(path, encoding="utf-8").read()
    boxes = []

    def add(pts, pad=0.0):
        if not pts:
            return
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        boxes.append((min(xs) - pad, min(ys) - pad, max(xs) + pad, max(ys) + pad))

    for fs, fe in geom.blocks(src, "\t(footprint "):
        blk = src[fs:fe]
        at = re.search(r"\n\t\t\(at ([-\d.]+) ([-\d.]+)(?: ([-\d.]+))?\)", blk)
        if not at:
            continue
        tf = _xf(float(at.group(1)), float(at.group(2)), float(at.group(3) or 0))
        for opener in ("\n\t\t(fp_line", "\n\t\t(fp_rect", "\n\t\t(fp_circle",
                       "\n\t\t(fp_poly", "\n\t\t(fp_text", "\n\t\t(property"):
            for bs, be in geom.blocks(blk, opener):
                b = blk[bs:be]
                if layer not in b:
                    continue
                pts = [tf(float(m.group(1)), float(m.group(2)))
                       for m in re.finditer(
                           r"\((?:start|end|center|mid|xy) ([-\d.]+) ([-\d.]+)\)", b)]
                if opener.endswith(("fp_text", "(property")):
                    # Reference/value text: approximate its extent from the string.
                    t = re.search(r'"([^"]*)"\s*\n?', b)
                    sz = re.search(r"\(size ([-\d.]+) ([-\d.]+)\)", b)
                    pat = re.search(r"\(at ([-\d.]+) ([-\d.]+)", b)
                    if not (t and sz and pat):
                        continue
                    if re.search(r"\(hide yes\)", b):
                        continue
                    cx, cy = tf(float(pat.group(1)), float(pat.group(2)))
                    h = float(sz.group(2))
                    w = max(1, len(t.group(1))) * float(sz.group(1)) * 0.85
                    boxes.append((cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2))
                    continue
                add(pts)
    # board-level text already placed
    for m in re.finditer(r"\n\t\(gr_text\b(?:.*?)\n\t\)", src, re.S):
        b = m.group(0)
        if layer not in b:
            continue
        t = re.search(r'gr_text "([^"]*)"', b)
        pat = re.search(r"\(at ([-\d.]+) ([-\d.]+)", b)
        sz = re.search(r"\(size ([-\d.]+) ([-\d.]+)\)", b)
        if not (t and pat and sz):
            continue
        cx, cy = float(pat.group(1)), float(pat.group(2))
        h = float(sz.group(2))
        w = max(1, len(t.group(1))) * float(sz.group(1)) * 0.85
        boxes.append((cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2))
    return boxes


def text_extent(text, size):
    """Approximate KiCad stroke-font extent. Deliberately generous."""
    return len(text) * size * 0.85, size * 1.4


def find_space(w, h, clearance=0.3, step=0.5, exclude=()):
    pads, _, _ = geom.load()
    boxes = [b for b in silk_boxes() if b not in exclude]
    X0, Y0, X1, Y1 = 100.0, 60.0, 200.0, 140.0
    edge = 2.0
    hw, hh = w / 2 + clearance, h / 2 + clearance
    out = []
    y = Y0 + edge + hh
    while y <= Y1 - edge - hh:
        x = X0 + edge + hw
        while x <= X1 - edge - hw:
            x0, y0, x1, y1 = x - hw, y - hh, x + hw, y + hh
            ok = True
            for a0, b0, a1, b1 in boxes:
                if not (a1 < x0 or a0 > x1 or b1 < y0 or b0 > y1):
                    ok = False
                    break
            if ok:
                for p in pads:
                    if not (p["x1"] < x0 or p["x0"] > x1 or p["y1"] < y0 or p["y0"] > y1):
                        ok = False
                        break
            if ok:
                out.append((x, y))
            x += step
        y += step
    return out


if __name__ == "__main__":
    w = float(sys.argv[1]) if len(sys.argv) > 1 else 20.0
    h = float(sys.argv[2]) if len(sys.argv) > 2 else 6.0
    boxes = silk_boxes()
    print("silkscreen items found: %d  (0 here means the layer filter is wrong again)"
          % len(boxes))
    spots = find_space(w, h)
    print("clear %.1f x %.1f mm centres: %d" % (w, h, len(spots)))
    for key, label in ((lambda p: (-p[1], p[0]), "lowest"),
                       (lambda p: (p[1], p[0]), "highest"),
                       (lambda p: (-p[0], -p[1]), "rightmost")):
        if spots:
            s = sorted(spots, key=key)[0]
            print("   %-10s (%.2f, %.2f)" % (label, s[0], s[1]))
