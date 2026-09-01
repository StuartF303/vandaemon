"""Whole-board drill clearance sweep, with TRUE oval-slot geometry.

Exists because three GND vias were found with drills physically overlapping J1's
0.6 x 1.7 mm shell slots, while KiCad DRC reported 0 errors and Konnect's
validate_for_manufacturing reported "READY: 0 issues".

Two reasons a check like this is needed on top of DRC:
  * KiCad's hole_to_hole severity is overridden to `warning` in this project, so
    drill defects never reach the error count.
  * Slot drills must be treated as rectangles. Approximating a 0.6 x 1.7 mm slot
    as a 0.6 mm circle understates its extent along the long axis and hides
    overlaps completely.

JLCPCB's standard 2-layer minimum hole-to-hole (edge to edge) is 0.50 mm.
Confirm against their current capability page before relying on it.

Usage: drill.py [limit_mm]      default 0.50
"""
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
BOARD = os.path.join(os.path.dirname(HERE), "VANDIMMER-4CH2A.kicad_pcb")
sys.path.insert(0, HERE)
import geom  # noqa: E402

JLC_MIN_HOLE_TO_HOLE = 0.50


def holes(path=BOARD):
    """Every drilled hole as (cx, cy, half_w, half_h, label). Round holes have hw==hh."""
    src = open(path, encoding="utf-8").read()
    out = []
    for m in re.finditer(r"\n\t\(via\b", src):
        j = src.find("(", m.start())
        depth, k = 0, j
        while k < len(src):
            if src[k] == "(":
                depth += 1
            elif src[k] == ")":
                depth -= 1
                if depth == 0:
                    break
            k += 1
        b = src[j:k + 1]
        at = re.search(r"\(at ([-\d.]+) ([-\d.]+)\)", b)
        dr = re.search(r"\(drill ([-\d.]+)\)", b)
        nt = re.search(r'\(net \d+ "?([^")]*)"?\)', b)
        if at and dr:
            r = float(dr.group(1)) / 2
            out.append((float(at.group(1)), float(at.group(2)), r, r,
                        "via %s" % (nt.group(1) if nt else "?")))
    for a, b in geom.blocks(src, "\t(footprint "):
        blk = src[a:b]
        ref = re.search(r'\(property "Reference" "([^"]+)"', blk)
        at = re.search(r"\n\t\t\(at ([-\d.]+) ([-\d.]+)(?: ([-\d.]+))?\)", blk)
        if not (ref and at):
            continue
        fx, fy = float(at.group(1)), float(at.group(2))
        rot = float(at.group(3) or 0)
        ang = math.radians(-rot)
        ca, sa = math.cos(ang), math.sin(ang)
        swap = abs(rot) % 180 == 90
        for m in re.finditer(r'\(pad "([^"]*)"[^\n]*?(?:thru_hole|np_thru_hole)(.*?)\n\t\t\)',
                             blk, re.S):
            g = m.group(0)
            pat = re.search(r"\(at ([-\d.]+) ([-\d.]+)", g)
            dr = re.search(r"\(drill (?:(oval) ([-\d.]+) ([-\d.]+)|([-\d.]+))", g)
            if not (pat and dr):
                continue
            if dr.group(1):
                w, h = float(dr.group(2)), float(dr.group(3))
            else:
                w = h = float(dr.group(4))
            if swap:
                w, h = h, w
            px, py = float(pat.group(1)), float(pat.group(2))
            out.append((fx + px * ca - py * sa, fy + px * sa + py * ca,
                        w / 2, h / 2, "%s.%s" % (ref.group(1), m.group(1))))
    return out


def conflicts(limit=JLC_MIN_HOLE_TO_HOLE, path=BOARD):
    hs = holes(path)
    bad = []
    for i in range(len(hs)):
        x1, y1, w1, h1, n1 = hs[i]
        for j in range(i + 1, len(hs)):
            x2, y2, w2, h2, n2 = hs[j]
            dx = max(abs(x1 - x2) - (w1 + w2), 0.0)
            dy = max(abs(y1 - y2) - (h1 + h2), 0.0)
            if dx == 0 and dy == 0:
                gap = -min((w1 + w2) - abs(x1 - x2), (h1 + h2) - abs(y1 - y2))
            else:
                gap = math.hypot(dx, dy)
            if gap < limit:
                bad.append((gap, n1, n2, x1, y1, x2, y2))
    return sorted(bad), len(hs)


if __name__ == "__main__":
    limit = float(sys.argv[1]) if len(sys.argv) > 1 else JLC_MIN_HOLE_TO_HOLE
    bad, n = conflicts(limit)
    over = [b for b in bad if b[0] < 0]
    print("drilled holes: %d   pairs closer than %.2f mm: %d   OVERLAPPING: %d"
          % (n, limit, len(bad), len(over)))
    for gap, n1, n2, x1, y1, x2, y2 in bad:
        tag = "  *** DRILLS OVERLAP ***" if gap < 0 else ""
        print("   %+7.3f mm  %-18s (%7.3f,%7.3f)  <->  %-18s (%7.3f,%7.3f)%s"
              % (gap, n1[:18], x1, y1, n2[:18], x2, y2, tag))
    sys.exit(1 if over else 0)
