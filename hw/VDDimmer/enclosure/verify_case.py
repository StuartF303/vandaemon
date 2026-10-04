"""Verify the enclosure against the geometry OpenSCAD actually produced.

Renders base and lid to STL, then measures the meshes. Nothing here reads a
constant and compares it with itself: the expected values come from OpenSCAD's own
derived parameters (echoed by case.scad), while the measurements come from the
triangles. A parameter that is right while the geometry that uses it is wrong --
an opening cut in the wrong wall, a post at the wrong pitch, a pillar across a
connector -- shows up only in that comparison.

    & "C:/Program Files/KiCad/10.0/bin/python.exe" verify_case.py

Exit code 0 means every property below holds.
"""
import json
import math
import os
import re
import struct
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OPENSCAD = r"C:\Program Files\OpenSCAD\openscad.exe"
SCAD = os.path.join(HERE, "case.scad")
OUT = os.environ.get("CASE_BUILD_DIR", os.path.join(HERE, "build"))

FAILED = []
CHECKED = []


def check(name, ok, detail=""):
    CHECKED.append(name)
    print("  %s  %s%s" % ("PASS" if ok else "FAIL", name, ("  -- " + detail) if detail else ""))
    if not ok:
        FAILED.append(name)
    return ok


# --------------------------------------------------------------------------- scad
def scad_params():
    """Derived values, straight out of case.scad."""
    echo_path = os.path.join(OUT, "params.echo")
    p = subprocess.run([OPENSCAD, "-o", echo_path, "-D", 'part="params"', SCAD],
                       capture_output=True, text=True)
    text = p.stdout + p.stderr
    if os.path.exists(echo_path):
        text += "\n" + open(echo_path, encoding="utf-8", errors="replace").read()
    blob = None
    for line in text.splitlines():
        m = re.search(r'ECHO:\s*"PARAMS (.*)"\s*$', line)
        if m:
            blob = m.group(1).replace('\\"', '"')
    if blob is None:
        sys.exit("case.scad did not echo its parameters:\n%s\n%s" % (p.stdout[-2000:], p.stderr[-2000:]))
    return json.loads(blob)


def render(part):
    path = os.path.join(OUT, part + ".stl")
    p = subprocess.run([OPENSCAD, "-o", path, "-D", 'part="%s"' % part, SCAD],
                       capture_output=True, text=True)
    if p.returncode or not os.path.exists(path):
        sys.exit("render of %s failed:\n%s\n%s" % (part, p.stdout[-3000:], p.stderr[-3000:]))
    return path


# --------------------------------------------------------------------------- mesh
class Mesh:
    def __init__(self, path):
        self.tris = self._load(path)
        xs = [v[0] for t in self.tris for v in t]
        ys = [v[1] for t in self.tris for v in t]
        zs = [v[2] for t in self.tris for v in t]
        self.bbox = (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs))
        self._index()

    @staticmethod
    def _load(path):
        data = open(path, "rb").read()
        if data[:5].lower() == b"solid" and b"facet" in data[:2048]:
            text = data.decode("utf-8", "replace")
            nums = []
            tris = []
            for m in re.finditer(r"vertex\s+(\S+)\s+(\S+)\s+(\S+)", text):
                nums.append(tuple(float(g) for g in m.groups()))
            for i in range(0, len(nums) - 2, 3):
                tris.append((nums[i], nums[i + 1], nums[i + 2]))
            return tris
        n = struct.unpack("<I", data[80:84])[0]
        tris = []
        off = 84
        for _ in range(n):
            vals = struct.unpack("<12fH", data[off:off + 50])
            tris.append((vals[3:6], vals[6:9], vals[9:12]))
            off += 50
        return tris

    def _index(self, cell=4.0):
        self.cell = cell
        self.grid = {}
        for t in self.tris:
            x0 = min(v[0] for v in t); x1 = max(v[0] for v in t)
            y0 = min(v[1] for v in t); y1 = max(v[1] for v in t)
            for ix in range(int(math.floor(x0 / cell)), int(math.floor(x1 / cell)) + 1):
                for iy in range(int(math.floor(y0 / cell)), int(math.floor(y1 / cell)) + 1):
                    self.grid.setdefault((ix, iy), []).append(t)

    def components(self):
        """Connected components over shared vertices (welded at 1 um)."""
        parent = {}

        def find(a):
            while parent[a] != a:
                parent[a] = parent[parent[a]]
                a = parent[a]
            return a

        def union(a, b):
            ra, rb = find(a), find(b)
            if ra != rb:
                parent[ra] = rb

        def key(v):
            return (round(v[0], 3), round(v[1], 3), round(v[2], 3))

        for t in self.tris:
            ks = [key(v) for v in t]
            for k in ks:
                parent.setdefault(k, k)
            union(ks[0], ks[1])
            union(ks[1], ks[2])
        return len({find(k) for k in parent})

    def volume(self):
        v = 0.0
        for a, b, c in self.tris:
            v += (a[0] * (b[1] * c[2] - c[1] * b[2])
                  - a[1] * (b[0] * c[2] - c[0] * b[2])
                  + a[2] * (b[0] * c[1] - c[0] * b[1])) / 6.0
        return abs(v)

    def inside(self, x, y, z):
        """Ray cast along +Z, re-cast if the ray grazes a triangle edge.

        A ray that passes exactly through a shared edge or vertex is counted by
        both neighbouring triangles or by neither, and the parity comes out wrong.
        That happens systematically, not rarely: any sample on the centre line of a
        cylinder lands on the spokes of its cap fan, so a solid ear tip reads as
        hollow. Nudging the ray a few microns off resolves it.
        """
        for dx, dy in ((0.0, 0.0), (1.7e-4, 1.1e-4), (-2.3e-4, 1.9e-4), (1.3e-4, -2.7e-4)):
            hits, grazed = self._cast(x + dx, y + dy, z)
            if not grazed:
                return hits % 2 == 1
        return hits % 2 == 1

    def _cast(self, x, y, z):
        hits = 0
        grazed = False
        for t in self.grid.get((int(math.floor(x / self.cell)), int(math.floor(y / self.cell))), ()):
            (x1, y1, z1), (x2, y2, z2), (x3, y3, z3) = t
            d1 = (x2 - x1, y2 - y1)
            d2 = (x3 - x1, y3 - y1)
            den = d1[0] * d2[1] - d1[1] * d2[0]
            if abs(den) < 1e-12:
                continue
            px, py = x - x1, y - y1
            u = (px * d2[1] - py * d2[0]) / den
            v = (d1[0] * py - d1[1] * px) / den
            if u < -1e-9 or v < -1e-9 or u + v > 1 + 1e-9:
                continue
            if min(abs(u), abs(v), abs(u + v - 1)) < 1e-7:
                grazed = True
            zt = z1 + u * (z2 - z1) + v * (z3 - z1)
            if zt > z + 1e-9:
                hits += 1
        return hits, grazed

    def material_in_box(self, x0, x1, y0, y1, z0, z1, n=3):
        """Any sample point inside the mesh within this box?"""
        pts = []
        for i in range(n):
            for j in range(n):
                for k in range(n):
                    fx = (i + 0.5) / n; fy = (j + 0.5) / n; fz = (k + 0.5) / n
                    pts.append((x0 + fx * (x1 - x0), y0 + fy * (y1 - y0), z0 + fz * (z1 - z0)))
        return [p for p in pts if self.inside(*p)]


# ---------------------------------------------------------------------- the checks
def main():
    os.makedirs(OUT, exist_ok=True)
    P = scad_params()
    print("derived by case.scad: outer %.1f x %.1f x %.1f mm, rim z=%.2f, board top z=%.2f, "
          "bridge %.2f mm" % (P["outer_w"], P["outer_d"], P["total_h"], P["z_rim"],
                              P["z_top"], P["bridge_h"]))

    base = Mesh(render("base"))
    lid = Mesh(render("lid_asm"))
    print("\nbase: %d triangles, %.1f cm3   lid: %d triangles, %.1f cm3"
          % (len(base.tris), base.volume() / 1000.0, len(lid.tris), lid.volume() / 1000.0))

    print("\n1. single connected solid")
    check("base is one piece", base.components() == 1,
          "%d components" % base.components())
    check("lid is one piece", lid.components() == 1,
          "%d components" % lid.components())

    print("\n2. outer envelope and thicknesses")
    bb = base.bbox
    # Ears reach beyond the shell in X; the shell itself sets Y and Z.
    check("base height matches z_rim", abs(bb[5] - P["z_rim"]) < 0.01,
          "mesh %.2f vs %.2f" % (bb[5], P["z_rim"]))
    check("base sits on z=0", abs(bb[4]) < 0.01, "min z %.3f" % bb[4])
    # The corner bosses straddle the corners in Y and the ears reach out in X, so
    # the envelope is the shell plus those, both derived by case.scad.
    check("base Y envelope = shell + corner bosses",
          abs(bb[2] + P["boss_reach"]) < 0.01
          and abs(bb[3] - (P["outer_d"] + P["boss_reach"])) < 0.01,
          "mesh y %.2f..%.2f, expected %.2f..%.2f"
          % (bb[2], bb[3], -P["boss_reach"], P["outer_d"] + P["boss_reach"]))
    check("base X envelope = shell + mounting ears",
          abs(bb[0] + P["ear_reach"]) < 0.01
          and abs(bb[1] - (P["outer_w"] + P["ear_reach"])) < 0.01,
          "mesh x %.2f..%.2f, expected %.2f..%.2f"
          % (bb[0], bb[1], -P["ear_reach"], P["outer_w"] + P["ear_reach"]))
    # A tangential joint still reads as one solid, so walk the load path from each
    # ear's tip into the wall and require material the whole way.
    for side, sign in (("left", -1), ("right", 1)):
        corner = 0 if sign < 0 else P["outer_w"]
        # from the screw hole's inboard edge (the path has to go round the hole,
        # not through it) to the middle of the wall
        x_tip = corner + sign * (P["ear_hole_off"] - P["ear_hole_d"] / 2 - 0.6)
        x_wall = (P["wall"] / 2) if sign < 0 else (P["outer_w"] - P["wall"] / 2)
        for y_name, y in (("front", P["ear_w"] / 2), ("back", P["outer_d"] - P["ear_w"] / 2)):
            n = 40
            gaps = [round(x_tip + (x_wall - x_tip) * i / (n - 1.0), 2)
                    for i in range(n)
                    if not base.inside(x_tip + (x_wall - x_tip) * i / (n - 1.0), y, P["ear_t"] / 2)]
            check("%s %s ear is continuous into the wall" % (y_name, side), gaps == [],
                  "" if not gaps else "no material at x=%s" % gaps[:5])

    check("ears stay below the lid joint",
          len(base.material_in_box(-P["ear_reach"] + 1, -P["boss_reach"] - 1,
                                   2, P["outer_d"] - 2,
                                   P["ear_t"] + 0.5, P["z_rim"])) == 0,
          "nothing outboard of the shell above the ear")
    check("floor is solid under the board",
          len(base.material_in_box(P["bx0"] + 20, P["bx0"] + 30, P["by0"] + 20,
                                   P["by0"] + 30, 0.4, P["floor_t"] - 0.4)) == 27,
          "floor sample")
    check("cavity above the board is empty",
          base.material_in_box(P["wall"] + 30, P["wall"] + 60, P["wall"] + 30,
                               P["wall"] + 60, P["z_top"] + 1, P["z_rim"] - 0.5) == [],
          "cavity sample")

    print("\n3. no case material inside any component's space")
    for p in P["parts"]:
        ref, role, x0, x1, y0, y1, h = p["ref"], p["role"], p["cx0"], p["cx1"], p["cy0"], p["cy1"], p["h"]
        hit = base.material_in_box(x0 + 0.2, x1 - 0.2, y0 + 0.2, y1 - 0.2,
                                   P["z_top"] + 0.2, P["z_top"] + h - 0.2)
        if not check("base clears %s (%s)" % (ref, role), hit == [],
                     "" if not hit else "%d points inside, e.g. %s" % (len(hit), tuple(round(v, 1) for v in hit[0]))):
            pass
        hit = lid.material_in_box(x0 + 0.2, x1 - 0.2, y0 + 0.2, y1 - 0.2,
                                  P["z_top"] + 0.2, P["z_top"] + h - 0.2)
        check("lid clears %s" % ref, hit == [],
              "" if not hit else "%d points inside" % len(hit))

    print("\n4. each harness connector has a clear path out through the front wall")
    for p in P["parts"]:
        if p["role"] != "harness":
            continue
        # a horizontal corridor at the connector's own height, through the wall
        hit = base.material_in_box(p["cx0"] + 0.5, p["cx1"] - 0.5, -0.5, P["wall"] + 0.5,
                                   P["z_top"] + 0.5, P["z_top"] + P["harness_h"] - 0.5, n=4)
        check("front wall open for %s" % p["ref"], hit == [],
              "" if not hit else "%d points of wall in the way" % len(hit))

    print("\n5. bridge above the slot, and pillars only in the gaps")
    check("material above the slot", len(base.material_in_box(
        P["slot_x0"] + 5, P["slot_x1"] - 5, 0.5, P["wall"] - 0.5,
        P["slot_z1"] + 0.3, P["z_rim"] - 0.3, n=4)) > 0, "bridge present")
    if P["letterbox_pillars"]:
        gaps = P["harness_gaps"]
        xs = [P["slot_x0"] + 0.5 + i * 0.5
              for i in range(int((P["slot_x1"] - P["slot_x0"] - 1) / 0.5))]
        z = (P["z_top"] + P["slot_z1"]) / 2
        bad = []
        for x in xs:
            if base.inside(x, P["wall"] / 2, z):
                if not any(g[0] - 0.05 <= x <= g[1] + 0.05 for g in gaps):
                    bad.append(round(x, 2))
        check("pillars lie only in connector gaps", bad == [],
              "" if not bad else "material at x=%s" % bad[:6])

    print("\n6. edge connector openings")
    j2 = [p for p in P["parts"] if p["role"] == "power_edge"][0]
    hit = base.material_in_box(j2["cx0"] + 0.5, j2["cx1"] - 0.5,
                               P["outer_d"] - P["wall"] + 0.5, P["outer_d"] - 0.5,
                               P["z_top"] + 0.5, P["z_top"] + P["power_h"] - 0.5, n=4)
    check("back wall open for J2", hit == [], "" if not hit else "%d points" % len(hit))
    j1 = [p for p in P["parts"] if p["role"] == "usb_edge"][0]
    hit = base.material_in_box(0.5, P["wall"] - 0.5, j1["cy0"] + 0.5, j1["cy1"] - 0.5,
                               P["z_top"] + 0.5, P["z_top"] + j1["h"] - 0.5, n=4)
    check("left wall open for J1", hit == [], "" if not hit else "%d points" % len(hit))

    print("\n7. board mounting posts")
    for hx, hy, drill in P["holes_case"]:
        top = base.material_in_box(hx - 1.0, hx + 1.0, hy - 1.0, hy + 1.0,
                                   P["z_board"] + 0.3, P["z_board"] + 1.0)
        check("pilot hole empty above post at (%.1f, %.1f)" % (hx, hy), top == [],
              "" if not top else "%d points" % len(top))
        ring = base.material_in_box(hx + P["post_d"] / 2 - 1.0, hx + P["post_d"] / 2 - 0.4,
                                    hy - 0.3, hy + 0.3,
                                    P["z_floor"] + 0.5, P["z_board"] - 0.3)
        check("post body present at (%.1f, %.1f)" % (hx, hy), len(ring) > 0, "post wall")
        above = base.material_in_box(hx - P["post_d"] / 2 + 0.6, hx + P["post_d"] / 2 - 0.6,
                                     hy - 0.5, hy + 0.5,
                                     P["z_board"] + 0.4, P["z_board"] + 1.2)
        check("nothing above post top at (%.1f, %.1f)" % (hx, hy), above == [],
              "post must stop at the board underside")

    print("\n8. lid features sit over what they serve")
    for role, what in (("jumper", "jumper hatch"), ("led", "LED window")):
        parts = [p for p in P["parts"] if p["role"] == role]
        x0 = min(p["cx0"] for p in parts); x1 = max(p["cx1"] for p in parts)
        y0 = min(p["cy0"] for p in parts); y1 = max(p["cy1"] for p in parts)
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        open_here = not lid.inside(cx, cy, P["z_rim"] + P["lid_t"] / 2)
        check("%s is open above (%.1f, %.1f)" % (what, cx, cy), open_here)

    print("\n9. lid and base do not fight each other")
    clash = []
    for i in range(60):
        f = (i + 0.5) / 60
        for (x, y) in ((P["wall"] + P["lip_clear"] + 0.1, P["wall"] + f * P["inner_d"]),
                       (P["wall"] + f * P["inner_w"], P["wall"] + P["lip_clear"] + 0.1)):
            z = P["z_rim"] - P["lip_h"] / 2
            if lid.inside(x, y, z) and base.inside(x, y, z):
                clash.append((round(x, 1), round(y, 1)))
    check("lip does not overlap the base walls", clash == [],
          "" if not clash else "%d clashing samples, e.g. %s" % (len(clash), clash[:4]))

    print("\n%d checks, %d failed" % (len(CHECKED), len(FAILED)))
    if FAILED:
        print("failed: " + "; ".join(FAILED))
        sys.exit(1)
    print("all properties hold")


if __name__ == "__main__":
    main()

