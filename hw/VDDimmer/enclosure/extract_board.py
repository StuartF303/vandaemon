"""Generate enclosure parameters from the board itself.

Every number the case needs -- outline, mounting holes, connector footprints and
their real heights -- is read from the KiCad board and its VRML export, then written
to an OpenSCAD include. Nothing is retyped, so the case cannot drift out of step
with the PCB, and a Rev B case is a re-run rather than a rewrite.

Usage (KiCad's Python, and KiCad must be closed):

    & "C:/Program Files/KiCad/10.0/bin/python.exe" extract_board.py \
        --board <path to .kicad_pcb> --out board_revA.scad

With no --board it extracts the `ordered-revA` tagged board from git, which is the
revision physically in the van.

Heights come from the VRML export's meshes. Parts whose 3D model is missing export
nothing, so their height would read 0 -- those fall back to the ASSUMED table below
and are marked `assumed` in the output. Trusting the 0 would put the lid through the
USB connector.
"""
import argparse
import math
import os
import re
import subprocess
import sys
import tempfile

KICAD_CLI = r"C:\Program Files\KiCad\10.0\bin\kicad-cli.exe"
NUM = r"[-+]?[0-9]*\.?[0-9]+(?:[eE][-+]?[0-9]+)?"

# Parts with no usable 3D model in the KiCad libraries, measured from datasheets.
# height = mm above the board's top surface.
ASSUMED = {
    "J1": (3.3, "USB-C receptacle body, HRO TYPE-C-31-M-12 datasheet"),
    "U1": (3.2, "ESP32-S3-WROOM-1U module incl. shield"),
    "L1": (3.0, "cjiang FXL0630, 6.6 x 7.0 x 3.0 mm"),
    "L3": (3.0, "cjiang FXL0630, 6.6 x 7.0 x 3.0 mm"),
    "U2": (1.1, "TSOT-23-6 body"),
    "D7": (1.6, "WS2812B PLCC4 5050 body"),
}

# Which references the enclosure cares about, and what each one needs.
ROLES = {
    "J3": "harness", "J4": "harness", "J5": "harness", "J6": "harness",
    "J7": "harness", "J8": "harness",
    "J2": "power_edge",
    "J1": "usb_edge",
    "J9": "jumper", "J10": "jumper",
    "D7": "led",
    "J11": "button", "J12": "button",
    "J13": "debug_header", "J14": "debug_header",
    "U1": "module",
    "U2": "hot", "L1": "hot", "L3": "hot",
    "Q1": "fets", "Q4": "fets",
}


def sh(cmd):
    p = subprocess.run(cmd, capture_output=True, text=True)
    if p.returncode:
        sys.exit("command failed: %s\n%s\n%s" % (cmd, p.stdout, p.stderr))
    return p.stdout


# --------------------------------------------------------------------------- board
def load_board(path):
    import pcbnew
    return pcbnew.LoadBoard(path)


def board_facts(board):
    import pcbnew
    M = pcbnew.ToMM
    bb = board.GetBoardEdgesBoundingBox()
    x0, y0 = M(bb.GetLeft()), M(bb.GetTop())
    facts = {
        "w": M(bb.GetWidth()), "h": M(bb.GetHeight()),
        "origin": (x0, y0),
        "holes": [], "parts": {},
    }
    for f in board.GetFootprints():
        ref = f.GetReference()
        for p in f.Pads():
            if p.GetAttribute() == pcbnew.PAD_ATTRIB_NPTH and M(p.GetDrillSizeX()) >= 2.0:
                facts["holes"].append((round(M(p.GetPosition().x) - x0, 3),
                                       round(M(p.GetPosition().y) - y0, 3),
                                       round(M(p.GetDrillSizeX()), 3)))
        if ref in ROLES:
            bx = f.GetBoundingBox(False)
            facts["parts"][ref] = {
                "role": ROLES[ref],
                "x0": round(M(bx.GetLeft()) - x0, 3), "x1": round(M(bx.GetRight()) - x0, 3),
                "y0": round(M(bx.GetTop()) - y0, 3), "y1": round(M(bx.GetBottom()) - y0, 3),
                "cx": round(M(f.GetPosition().x) - x0, 3),
                "cy": round(M(f.GetPosition().y) - y0, 3),
            }
    facts["holes"].sort()
    return facts


# ---------------------------------------------------------------------------- VRML
def ident():
    return [[1.0 if i == j else 0.0 for j in range(4)] for i in range(4)]


def mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def local(n):
    tx, ty, tz = n["translation"]
    ax, ay, az, ang = n["rotation"]
    sx, sy, sz = n["scale"]
    t = ident(); t[0][3], t[1][3], t[2][3] = tx, ty, tz
    r = ident()
    norm = math.sqrt(ax * ax + ay * ay + az * az)
    if norm > 1e-12 and abs(ang) > 1e-12:
        ax, ay, az = ax / norm, ay / norm, az / norm
        c, s, C = math.cos(ang), math.sin(ang), 1 - math.cos(ang)
        r = [[ax * ax * C + c, ax * ay * C - az * s, ax * az * C + ay * s, 0],
             [ay * ax * C + az * s, ay * ay * C + c, ay * az * C - ax * s, 0],
             [az * ax * C - ay * s, az * ay * C + ax * s, az * az * C + c, 0],
             [0, 0, 0, 1]]
    sc = ident(); sc[0][0], sc[1][1], sc[2][2] = sx, sy, sz
    return mul(mul(t, r), sc)


TOK = re.compile(
    r"(?P<txfm>Transform\s*\{)"
    r"|(?P<defshape>DEF\s+(?P<defname>\w+)\s+Shape\s*\{)"
    r"|(?P<use>USE\s+(?P<usename>\w+))"
    r"|(?P<open>\{)|(?P<close>\})"
    r"|translation\s+(?P<tx>{n})\s+(?P<ty>{n})\s+(?P<tz>{n})"
    r"|rotation\s+(?P<rx>{n})\s+(?P<ry>{n})\s+(?P<rz>{n})\s+(?P<ra>{n})"
    r"|scaleOrientation\s+{n}\s+{n}\s+{n}\s+{n}"
    r"|scale\s+(?P<sx>{n})\s+(?P<sy>{n})\s+(?P<sz>{n})"
    r"|point\s*\[(?P<points>[^\]]*)\]".replace("{n}", NUM))


def vrml_groups(path):
    """Bounding box per top-level model group, in mm, Y flipped relative to the board.

    Two traps, both of which produce believable but wrong numbers:

      * A VRML node's fields follow its opening brace, so they belong to that node
        and not to the child after it. Getting it wrong shifts every model by its
        neighbour's transform.
      * KiCad emits each distinct component subtree once as `DEF TXFM_n` and
        instances the repeats as `USE TXFM_n`. Ignoring USE leaves every repeated
        part -- three of the four lamp connectors, five of the six headers -- with no
        geometry at all, reading as height zero.

    Each Transform node accumulates a bounding box expressed in its PARENT's frame,
    so a referenced subtree can be re-instantiated anywhere by transforming the eight
    corners of the box it recorded.
    """
    EMPTY = [1e9, -1e9, 1e9, -1e9, 1e9, -1e9]
    stack, groups, defs, gid = [], {}, {}, 0
    text = open(path, encoding="utf-8", errors="replace").read()

    def corners(b):
        return [(x, y, z) for x in (b[0], b[1]) for y in (b[2], b[3]) for z in (b[4], b[5])]

    def innermost():
        for n in reversed(stack):
            if n["kind"] == "T":
                return n
        return None

    def contribute(points, node):
        """Points are in `node`'s child frame; record them in node's parent frame."""
        mat = local(node)
        box = node["bbox"]
        for p in points:
            x = mat[0][0] * p[0] + mat[0][1] * p[1] + mat[0][2] * p[2] + mat[0][3]
            y = mat[1][0] * p[0] + mat[1][1] * p[1] + mat[1][2] * p[2] + mat[1][3]
            z = mat[2][0] * p[0] + mat[2][1] * p[1] + mat[2][2] * p[2] + mat[2][3]
            box[0] = min(box[0], x); box[1] = max(box[1], x)
            box[2] = min(box[2], y); box[3] = max(box[3], y)
            box[4] = min(box[4], z); box[5] = max(box[5], z)

    for m in TOK.finditer(text):
        if m.group("txfm"):
            # "DEF TXFM_19 Transform {" names the node; a bare "Transform {" does not.
            name = None
            back = text.rfind("DEF ", max(0, m.start() - 40), m.start())
            if back != -1:
                between = text[back + 4:m.start()].split()
                name = between[0] if len(between) == 1 else None
            stack.append({"translation": (0, 0, 0), "rotation": (0, 0, 1, 0),
                          "scale": (1, 1, 1), "kind": "T", "name": name,
                          "bbox": list(EMPTY), "depth": sum(1 for n in stack if n["kind"] == "T") + 1})
        elif m.group("use"):
            name, node = m.group("usename"), innermost()
            if name in defs and node is not None:
                if defs[name][0] < defs[name][1]:
                    contribute(corners(defs[name]), node)
                    if node["depth"] == 1:            # a whole component reused at top level
                        gid += 1
                        b = list(EMPTY)
                        mat = local(node)
                        for p in corners(defs[name]):
                            x = mat[0][0] * p[0] + mat[0][1] * p[1] + mat[0][2] * p[2] + mat[0][3]
                            y = mat[1][0] * p[0] + mat[1][1] * p[1] + mat[1][2] * p[2] + mat[1][3]
                            z = mat[2][0] * p[0] + mat[2][1] * p[1] + mat[2][2] * p[2] + mat[2][3]
                            b[0] = min(b[0], x); b[1] = max(b[1], x)
                            b[2] = min(b[2], y); b[3] = max(b[3], y)
                            b[4] = min(b[4], z); b[5] = max(b[5], z)
                        groups[gid] = b
        elif m.group("defshape") or m.group("open"):
            # `DEF SHAPE_n Shape {` opens a node like any other brace. Matching it as a
            # distinct token and then not pushing it lost one level of nesting here,
            # which cascaded: every later pop closed the wrong node.
            stack.append({"kind": "x"})
        elif m.group("close"):
            if not stack:
                continue
            node = stack.pop()
            if node["kind"] != "T":
                continue
            if node["name"]:
                defs[node["name"]] = list(node["bbox"])
            parent = innermost()
            if node["bbox"][0] > node["bbox"][1]:
                continue
            if parent is not None:
                if node["depth"] == 2:                # one component, in root frame
                    gid += 1
                    b = list(EMPTY)
                    mat = local(parent)
                    for p in corners(node["bbox"]):
                        x = mat[0][0] * p[0] + mat[0][1] * p[1] + mat[0][2] * p[2] + mat[0][3]
                        y = mat[1][0] * p[0] + mat[1][1] * p[1] + mat[1][2] * p[2] + mat[1][3]
                        z = mat[2][0] * p[0] + mat[2][1] * p[1] + mat[2][2] * p[2] + mat[2][3]
                        b[0] = min(b[0], x); b[1] = max(b[1], x)
                        b[2] = min(b[2], y); b[3] = max(b[3], y)
                        b[4] = min(b[4], z); b[5] = max(b[5], z)
                    groups[gid] = b
                contribute(corners(node["bbox"]), parent)
        elif m.group("tx") is not None and stack and stack[-1]["kind"] == "T":
            stack[-1]["translation"] = tuple(float(m.group(k)) for k in ("tx", "ty", "tz"))
        elif m.group("ra") is not None and stack and stack[-1]["kind"] == "T":
            stack[-1]["rotation"] = tuple(float(m.group(k)) for k in ("rx", "ry", "rz", "ra"))
        elif m.group("sz") is not None and stack and stack[-1]["kind"] == "T":
            stack[-1]["scale"] = tuple(float(m.group(k)) for k in ("sx", "sy", "sz"))
        elif m.group("points") is not None:
            node = innermost()
            if node is not None:
                vals = [float(v) for v in re.findall(NUM, m.group("points"))]
                pts = [(vals[i], vals[i + 1], vals[i + 2]) for i in range(0, len(vals) - 2, 3)]
                contribute(pts, node)
                if node["depth"] == 1:
                    # KiCad emits the PCB slab as shapes directly under the root, so it
                    # is never a component group. Keep it as group 0 -- the case needs
                    # it to know where the board's top surface is.
                    mat = local(node)
                    box = groups.setdefault(0, list(EMPTY))
                    for p in pts:
                        x = mat[0][0] * p[0] + mat[0][1] * p[1] + mat[0][2] * p[2] + mat[0][3]
                        y = mat[1][0] * p[0] + mat[1][1] * p[1] + mat[1][2] * p[2] + mat[1][3]
                        z = mat[2][0] * p[0] + mat[2][1] * p[1] + mat[2][2] * p[2] + mat[2][3]
                        box[0] = min(box[0], x); box[1] = max(box[1], x)
                        box[2] = min(box[2], y); box[3] = max(box[3], y)
                        box[4] = min(box[4], z); box[5] = max(box[5], z)
    return groups


def measure_heights(facts, wrl):
    """Height above the board's top surface for every part the case cares about."""
    groups = vrml_groups(wrl)
    board = max(groups.values(), key=lambda b: (b[1] - b[0]) * (b[3] - b[2]))
    top = board[5]
    bw, bh = facts["w"], facts["h"]
    if abs((board[1] - board[0]) - bw) > 1.0 or abs((board[3] - board[2]) - bh) > 1.0:
        sys.exit("VRML board body %.1f x %.1f does not match outline %.1f x %.1f"
                 % (board[1] - board[0], board[3] - board[2], bw, bh))

    def to_world(x, y):          # board mm -> VRML mm; VRML has Y up
        return board[0] + x, board[2] + (bh - y)

    missing = []
    for ref, part in facts["parts"].items():
        wx0, wy1 = to_world(part["x0"], part["y0"])
        wx1, wy0 = to_world(part["x1"], part["y1"])
        best = 0.0
        for b in groups.values():
            if b is board:
                continue
            cx, cy = (b[0] + b[1]) / 2, (b[2] + b[3]) / 2
            if wx0 - 0.5 <= cx <= wx1 + 0.5 and wy0 - 0.5 <= cy <= wy1 + 0.5:
                best = max(best, b[5] - top)
        if ref in ASSUMED and best < ASSUMED[ref][0]:
            part["height"] = ASSUMED[ref][0]
            part["height_src"] = "assumed: " + ASSUMED[ref][1]
        elif best <= 0.01:
            missing.append(ref)
        else:
            part["height"] = round(best, 2)
            part["height_src"] = "measured from 3D model"
    if missing:
        sys.exit("no 3D model and no ASSUMED height for: %s\nAdd each to ASSUMED before "
                 "the case is built around a zero." % ", ".join(sorted(missing)))
    return facts


# --------------------------------------------------------------------------- output
def scad_vector(seq):
    return "[" + ", ".join("%.3f" % v for v in seq) + "]"


def write_scad(facts, out, board_path, revision):
    lines = [
        "// GENERATED by extract_board.py -- do not edit.",
        "// source: %s" % os.path.basename(board_path),
        "// revision: %s" % revision,
        "//",
        "// Board coordinates: origin at the outline's top-left corner as KiCad draws it,",
        "// x right, y DOWN. case.scad converts y with by().",
        "",
        "pcb_w = %.3f;" % facts["w"],
        "pcb_h = %.3f;" % facts["h"],
        "pcb_t = 1.6;",
        "",
        "// [x, y, drill] per mounting hole",
        "pcb_holes = [",
    ]
    for x, y, d in facts["holes"]:
        lines.append("  [%.3f, %.3f, %.3f]," % (x, y, d))
    lines += ["];", ""]
    lines.append("// [name, role, x0, x1, y0, y1, cx, cy, height_above_board]")
    lines.append("pcb_parts = [")
    for ref in sorted(facts["parts"], key=lambda r: (facts["parts"][r]["role"], r)):
        p = facts["parts"][ref]
        lines.append('  ["%s", "%s", %.3f, %.3f, %.3f, %.3f, %.3f, %.3f, %.2f],  // %s'
                     % (ref, p["role"], p["x0"], p["x1"], p["y0"], p["y1"],
                        p["cx"], p["cy"], p["height"], p["height_src"]))
    lines += ["];", ""]
    lines += [
        "// Helpers over pcb_parts",
        "function part_row(ref) = pcb_parts[search([ref], pcb_parts)[0]];",
        "function parts_with(role) = [for (p = pcb_parts) if (p[1] == role) p];",
        "function role_x0(role) = min([for (p = parts_with(role)) p[2]]);",
        "function role_x1(role) = max([for (p = parts_with(role)) p[3]]);",
        "function role_y0(role) = min([for (p = parts_with(role)) p[4]]);",
        "function role_y1(role) = max([for (p = parts_with(role)) p[5]]);",
        "function role_h(role)  = max([for (p = parts_with(role)) p[8]]);",
        "function all_parts_h() = max([for (p = pcb_parts) p[8]]);",
        "",
    ]
    open(out, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--board")
    ap.add_argument("--tag", default="ordered-revA")
    ap.add_argument("--out", default=os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                  "board_revA.scad"))
    args = ap.parse_args()

    tmp = tempfile.mkdtemp(prefix="vddimmer-case-")
    revision = args.board or ("git tag " + args.tag)
    board_path = args.board
    if not board_path:
        repo = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
        board_path = os.path.join(tmp, "VANDIMMER-4CH2A.kicad_pcb")
        for name in ("VANDIMMER-4CH2A.kicad_pcb", "VANDIMMER-4CH2A.kicad_pro"):
            blob = sh(["git", "-C", repo, "show", "%s:hw/VDDimmer/%s" % (args.tag, name)])
            open(os.path.join(tmp, name), "w", encoding="utf-8", newline="\n").write(blob)

    facts = board_facts(load_board(board_path))
    wrl = os.path.join(tmp, "board.wrl")
    sh([KICAD_CLI, "pcb", "export", "vrml", "--output", wrl, "--units", "mm", board_path])
    facts = measure_heights(facts, wrl)

    out = write_scad(facts, args.out, board_path, revision)
    print("board %.1f x %.1f mm, %d mounting holes, %d parts"
          % (facts["w"], facts["h"], len(facts["holes"]), len(facts["parts"])))
    for ref in sorted(facts["parts"], key=lambda r: -facts["parts"][r]["height"]):
        p = facts["parts"][ref]
        print("  %-4s %-14s %6.2f mm  %s" % (ref, p["role"], p["height"], p["height_src"]))
    print("wrote " + out)


if __name__ == "__main__":
    main()
