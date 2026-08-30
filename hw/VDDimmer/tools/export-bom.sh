#!/usr/bin/env bash
# VANDIMMER-4CH+2A BOM export.
#
# Group by Value+Footprint, NOT by MPN. Konnect's edit_schematic_component writes
# fields to unit 1 only of a multi-unit symbol, where KiCad's GUI propagates them to
# every unit. U4 and U5 are 5-unit 74HCT125s, so units 2-5 carry no MPN and grouping
# by MPN splits U5 across two lines -- a BOM that orders 3 of a 2-off part. Grouping
# by Value+Footprint collapses them correctly and still reports the MPN from unit 1.
#
# The inconsistency self-heals the moment anyone edits those fields in the KiCad GUI --
# but ONLY if the dialog is opened on unit 1. KiCad's Symbol Properties dialog shows the
# clicked unit's fields and writes them to every unit on OK, so opening it on a unit that
# lacks MPN/LCSC propagates the *absence* and wipes unit 1. That is how U4 lost all three
# sourcing fields; U5, clicked on unit 1, propagated correctly.
#
# ${DNP} is a grouping key as well as a column. Without it a DNP part collapses into the
# same line as its fitted siblings and the whole line reads "DNP" -- R11/R12 grouped with
# R9/R10/R13 and told the assembler not to fit any of the five.
set -u
SCH="${1:-C:/Projects/vandaemon/hw/VDDimmer/VANDIMMER-4CH2A.kicad_sch}"
OUT="${2:-$(dirname "$0")/../VANDIMMER-4CH2A-bom.csv}"
"/c/Program Files/KiCad/10.0/bin/kicad-cli.exe" sch export bom \
    --fields 'Reference,Value,Footprint,MPN,LCSC,Manufacturer,${QUANTITY},${DNP}' \
    --group-by 'Value,Footprint,${DNP}' \
    --output "$OUT" "$SCH"
echo "lines: $(($(grep -c '' "$OUT") - 1))"
