"""Zip the gerbers and re-check the assembly data before upload.

Every check here exists because something upstream reported success while being
wrong: export_manufacturing_package said "No warnings" over a 98-row CPL in
inches with no LCSC column, and set_board_size reported a resized board while
appending a second outline.

Exits non-zero if the BOM and CPL disagree or any line lacks an LCSC code.
"""
import csv
import io
import os
import re
import sys
import zipfile


def main(tmp, out):
    zpath = os.path.join(out, "VANDIMMER-4CH2A-gerbers.zip")
    with zipfile.ZipFile(zpath, "w", zipfile.ZIP_DEFLATED) as z:
        for name in sorted(os.listdir(tmp)):
            # kicad-cli drops an org.kicad.kicad/ cache dir in the output folder
            if not os.path.isfile(os.path.join(tmp, name)):
                continue
            z.write(os.path.join(tmp, name), name)
        names = z.namelist()
    print("gerber zip : %d files" % len(names))
    for n in names:
        print("   %s" % n)

    edge = [n for n in names if "Edge_Cuts" in n]
    if edge:
        t = io.open(os.path.join(tmp, edge[0]), encoding="utf-8", errors="replace").read()
        m = re.search(r"%FSLAX(\d)(\d)", t)
        dec = int(m.group(2)) if m else 6
        pts = [(int(a) / 10 ** dec, int(b) / 10 ** dec)
               for a, b in re.findall(r"^X(-?\d+)Y(-?\d+)D0[12]\*", t, re.M)]
        if pts:
            xs = [p[0] for p in pts]
            ys = [p[1] for p in pts]
            print("board size : %.2f x %.2f mm" % (max(xs) - min(xs), max(ys) - min(ys)))

    holes = 0
    for n in names:
        if n.endswith(".drl"):
            t = io.open(os.path.join(tmp, n), encoding="utf-8", errors="replace").read()
            c = len(re.findall(r"^X[-\d.]+Y", t, re.M))
            holes += c
            print("drill      : %-42s %d holes" % (n, c))
    print("drill total: %d" % holes)

    cpl = {r["Designator"]
           for r in csv.DictReader(io.open(os.path.join(out, "VANDIMMER-4CH2A-CPL.csv"),
                                           encoding="utf-8"))}
    bom, nolcsc = set(), []
    for r in csv.DictReader(io.open(os.path.join(out, "VANDIMMER-4CH2A-BOM.csv"),
                                    encoding="utf-8")):
        bom.update(d.strip() for d in r["Designator"].split(","))
        if not r["LCSC Part #"].strip():
            nolcsc.append(r["Designator"])
    orphans = sorted(cpl ^ bom)
    print("CPL / BOM  : %d / %d designators" % (len(cpl), len(bom)))
    print("orphans    : %s" % (", ".join(orphans) if orphans else "none"))
    print("no LCSC    : %s" % (", ".join(nolcsc) if nolcsc else "none"))

    # One LCSC code must appear on exactly one BOM line. JLC keys on the part number:
    # a code on two lines leaves it unable to resolve the quantity, so it sets Qty 0
    # and unticks BOTH rows. That silently dropped eight lines on the first upload and
    # looked like an out-of-stock problem.
    codes = {}
    for r in csv.DictReader(io.open(os.path.join(out, "VANDIMMER-4CH2A-BOM.csv"),
                                    encoding="utf-8")):
        codes.setdefault(r["LCSC Part #"].strip(), []).append(r["Designator"])
    dups = {k: v for k, v in codes.items() if len(v) > 1}
    print("dup LCSC   : %s" % (", ".join(dups) if dups else "none"))
    for k, v in dups.items():
        print("   %s appears on %d lines: %s" % (k, len(v), " | ".join(v)))

    return 1 if orphans or nolcsc or dups else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1], sys.argv[2]))
