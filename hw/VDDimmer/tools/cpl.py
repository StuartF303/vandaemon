"""VANDIMMER-4CH+2A assembly-data export: JLCPCB CPL + JLCPCB BOM.

Why this exists
---------------
`kicad-cli pcb export pos --smd-only` returns ZERO rows on this board, because no
footprint carries KiCad's `(attr smd)` flag and nothing in the MCP toolset can set it.
The unfiltered export returns all 96 footprints, including four M3 mounting holes and
every hand-soldered connector, so neither export is a usable CPL on its own.

Rather than hand-editing 96 footprint types in the GUI, the exclusion set is derived
from `bom-lcsc.csv` -- the same sheet that drives sourcing. A part is machine-placed
iff its BOM line names a JLC library tier. That makes the CPL and the BOM agree by
construction: a part cannot be quoted-and-not-placed, or placed-and-not-quoted.

Two sheets, two jobs
--------------------
The schematic is authoritative for what a part IS -- Value, footprint, Assembly note.
`bom-lcsc.csv` is authoritative for where it is BOUGHT -- LCSC code, library tier,
stock, price. This script reads each for what it owns and cross-checks the overlap,
because the sheets have drifted before: F1 was corrected to 8 A and U3 to a BL1117 in
the schematic while `bom-lcsc.csv` still named the 10 A fuse and the AMS1117.

Exclusions
----------
  blank JLC_Library      hand-fit connectors and mechanical parts (spec 10)
  DNP                    not fitted

NOTE ON DNP: no MCP tool writes KiCad's `(dnp yes)` attribute, so this board has no
real DNP flag on any symbol -- `${DNP}` is empty for all 96. DNP is therefore taken
from the `Assembly` field (which begins "DNP - ...") or from a "DNP" marker in the
sourcing sheet's Value. All three sources are checked; `${DNP}` becomes authoritative
on its own the moment someone sets the flag in the GUI.

NOTE ON ROTATION: KiCad's rotation and JLC's part-library rotation disagree for many
package families. This script does NOT apply a correction table. Check the rendered
placement preview in JLC's uploader before releasing.

Usage:  "C:/Program Files/KiCad/10.0/bin/python.exe" tools/cpl.py
"""
import csv
import os
import re
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
PCB = os.path.join(ROOT, "VANDIMMER-4CH2A.kicad_pcb")
BOM_LCSC = os.path.join(ROOT, "bom-lcsc.csv")
SCH = os.path.join(ROOT, "VANDIMMER-4CH2A.kicad_sch")
OUT_CPL = os.path.join(ROOT, "VANDIMMER-4CH2A-cpl.csv")
OUT_BOM = os.path.join(ROOT, "VANDIMMER-4CH2A-jlc-bom.csv")
KICAD_CLI = "C:/Program Files/KiCad/10.0/bin/kicad-cli.exe"


def expand_refs(field):
    """Expand a BOM Refs cell: 'C1,C3-C5' -> ['C1','C3','C4','C5']."""
    out = []
    for tok in field.split(","):
        tok = tok.strip()
        if not tok:
            continue
        m = re.match(r"^([A-Za-z]+)(\d+)-(?:[A-Za-z]+)?(\d+)$", tok)
        if m:
            prefix, first, last = m.group(1), int(m.group(2)), int(m.group(3))
            out.extend("%s%d" % (prefix, i) for i in range(first, last + 1))
        else:
            out.append(tok)
    return out


def export_positions():
    """Unfiltered position export. --smd-only would return nothing; see module docstring."""
    fd, path = tempfile.mkstemp(suffix=".csv")
    os.close(fd)
    subprocess.run(
        [KICAD_CLI, "pcb", "export", "pos", "--format", "csv", "--units", "mm",
         "--side", "both", "-o", path, PCB],
        check=True, capture_output=True,
    )
    with open(path, newline="") as fh:
        rows = list(csv.DictReader(fh))
    os.unlink(path)
    return rows


def export_schematic_bom():
    """Per-reference schematic BOM. Exported fresh so it can never read a stale file."""
    fd, path = tempfile.mkstemp(suffix=".csv")
    os.close(fd)
    subprocess.run(
        [KICAD_CLI, "sch", "export", "bom",
         "--fields", "Reference,Value,Footprint,LCSC,Assembly,${DNP}",
         "--group-by", "",
         "--output", path, SCH],
        check=True, capture_output=True,
    )
    with open(path, newline="", encoding="utf-8") as fh:
        rows = list(csv.DictReader(fh))
    os.unlink(path)
    out = {}
    for row in rows:
        for ref in expand_refs(row["Reference"]):
            out[ref] = row
    return out


def dnp_source(sch_row, bom_row):
    """Which of the three places records this part as DNP, or None. Reporting the
    source matters: only KiCad's own attribute also keeps it out of the KiCad BOM."""
    if sch_row is not None:
        if sch_row.get("DNP", "").strip():
            return "kicad-attr"
        if sch_row.get("Assembly", "").strip().upper().startswith("DNP"):
            return "assembly-field"
    if "DNP" in bom_row["Value"].upper():
        return "sourcing-sheet"
    return None


def main():
    bom = {}
    with open(BOM_LCSC, newline="") as fh:
        for line in csv.DictReader(fh):
            for ref in expand_refs(line["Refs"]):
                bom[ref] = line

    schematic = export_schematic_bom()
    positions = export_positions()
    placed, excluded, unclassified, drift = [], [], [], []

    for row in positions:
        ref = row["Ref"]
        line = bom.get(ref)
        if line is None:
            unclassified.append(ref)
            continue
        sch_row = schematic.get(ref)

        # The schematic owns Value; carry it into the BOM Comment so a corrected part
        # can never be quoted under its superseded name.
        if sch_row is not None:
            sch_value = sch_row["Value"].strip()
            src_value = line["Value"].strip()
            if sch_value and sch_value != src_value:
                normalised = src_value.upper().replace("DNP", "").strip()
                if normalised != sch_value.upper() and line["JLC_Library"].strip():
                    drift.append((ref, sch_value, src_value))
            line = dict(line, Value=sch_value or src_value)
            if sch_row.get("LCSC", "").strip() and line["LCSC"].strip()                     and sch_row["LCSC"].strip() != line["LCSC"].strip():
                drift.append((ref, "LCSC " + sch_row["LCSC"], "LCSC " + line["LCSC"]))

        if not bom[ref]["JLC_Library"].strip():
            excluded.append((ref, "hand-fit / mechanical", None))
        else:
            src = dnp_source(sch_row, bom[ref])
            if src:
                excluded.append((ref, "DNP", src))
            else:
                placed.append((row, line))

    if unclassified:
        sys.stderr.write(
            "REFUSING TO WRITE: %d footprint(s) on the board have no bom-lcsc.csv line, "
            "so they can be neither placed nor excluded deliberately: %s\n"
            % (len(unclassified), ", ".join(sorted(unclassified)))
        )
        return 1

    with open(OUT_CPL, "w", newline="") as fh:
        w = csv.writer(fh)
        w.writerow(["Designator", "Mid X", "Mid Y", "Layer", "Rotation"])
        for row, _ in placed:
            w.writerow([
                row["Ref"],
                "%.4fmm" % float(row["PosX"]),
                "%.4fmm" % float(row["PosY"]),
                "Top" if row["Side"] == "top" else "Bottom",
                "%.4f" % (float(row["Rot"]) % 360.0),
            ])

    seen, jlc_lines = set(), []
    for _, line in placed:
        key = (line["Value"], line["Footprint"])
        if key in seen:
            continue
        seen.add(key)
        refs = [r["Ref"] for r, l in placed if (l["Value"], l["Footprint"]) == key]
        jlc_lines.append((line, sorted(refs, key=lambda s: (re.sub(r"\d", "", s),
                                                           int(re.sub(r"\D", "", s) or 0)))))

    with open(OUT_BOM, "w", newline="") as fh:
        w = csv.writer(fh)
        w.writerow(["Comment", "Designator", "Footprint", "LCSC Part #"])
        for line, refs in jlc_lines:
            w.writerow([line["Value"], ",".join(refs), line["Footprint"], line["LCSC"]])

    missing_lcsc = [(l["Value"], ",".join(r)) for l, r in jlc_lines if not l["LCSC"].strip()]

    print("=== VANDIMMER-4CH+2A assembly data ===")
    print("footprints on board : %d" % len(positions))
    print("machine-placed      : %d  -> %s" % (len(placed), os.path.basename(OUT_CPL)))
    print("BOM lines           : %d  -> %s" % (len(jlc_lines), os.path.basename(OUT_BOM)))
    print("excluded            : %d" % len(excluded))
    for reason in ("hand-fit / mechanical", "DNP"):
        refs = sorted(r for r, why, _ in excluded if why == reason)
        if refs:
            print("    %-22s %2d  %s" % (reason, len(refs), ", ".join(refs)))
    sides = set(r["Side"] for r, _ in placed)
    print("sides               : %s" % ", ".join(sorted(sides)))

    if missing_lcsc:
        print()
        print("WARNING: %d machine-placed line(s) carry no LCSC code and cannot be "
              "assembled:" % len(missing_lcsc))
        for value, refs in missing_lcsc:
            print("    %-22s %s" % (value, refs))

    if drift:
        print()
        print("WARNING: %d field(s) disagree between the schematic and bom-lcsc.csv. "
              "The schematic wins in the output above; reconcile the sourcing sheet:"
              % len(drift))
        for ref, sch_value, src_value in drift:
            print("    %-5s schematic=%-24s bom-lcsc=%s" % (ref, sch_value, src_value))

    weak = sorted(r for r, why, src in excluded if why == "DNP" and src != "kicad-attr")
    if weak:
        print()
        print("NOTE: %s are excluded on a text marker only, NOT on KiCad's (dnp yes) "
              "attribute -- so the KiCad BOM still quotes them. Set it in the GUI "
              "(Symbol Properties -> Do not populate)." % ", ".join(weak))
    strong = sorted(r for r, why, src in excluded if src == "kicad-attr")
    if strong:
        print()
        print("DNP via KiCad's own (dnp yes) attribute: %s -- BOM and CPL agree."
              % ", ".join(strong))
    print()
    print("Rotation is KiCad's, uncorrected for JLC's part-library conventions. "
          "Check the placement preview in JLC's uploader before releasing.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
