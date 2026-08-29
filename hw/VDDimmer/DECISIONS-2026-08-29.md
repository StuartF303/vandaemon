# Design decisions — 2026-08-29

Taken with Stuart during the fab-readiness pass. These settle the open items listed in
`HANDOFF.md` under "Known open items". **Do not re-litigate.**

## Component changes

| Ref | Was | Now | Layout impact |
|---|---|---|---|
| F1 | "10A 250V" 2410 fuse | **8 A**, `LTC2410-1800TSK`, LCSC **C54824501**, 250 V, 50 A interrupt, I²t 115.2 | none — same 2410 footprint |
| U3 | AMS1117-3.3 (5 mA Iq) | **BL1117-33CX**, LCSC **C5400**, SOT-223, 2 mA Iq, 1 A, dropout 1.5 V @ 1 A | none — same SOT-223 |
| R30 | 0R 0805, unspecified | 0R 0805 **rated ≥ 2 A** | none — footprint kept |
| — | no I2C pull-ups | **add 2 × 4.7 k to +3V3** on SDA/SCL near J13 | schematic + placement + routing |
| L3, C20, R31 | DNP in text only | **fitted** — input LC filter populated | none |
| R11, R12 | DNP in text only | **stay DNP**, but set KiCad's real `(dnp yes)` flag | none |
| J2 | Value "VIN 9-24V" | **12 V only** — correct the label | silkscreen only |

## Reasoning worth keeping

**F1 is a fuse, not a PPTC.** `HANDOFF.md` described it as "a 10 A PPTC on a 5 A board";
its footprint is `Fuse_2410_6125Metric` and its value "10A 250V" is fuse nomenclature —
PPTCs are rated 6-60 V. The board's actual PPTCs are PF1/PF2 ("PTC 2A 30V", 1812) on the
addressable outputs. This matters because **no 8 A PPTC exists in SMD at all** — JLC's
resettable range tops out at 5 A hold / 10 A trip in 2920, a much larger footprint. As a
fuse the part was easy: 8 A in the same 2410, in stock. With the usual 25 % continuous
derating an 8 A fuse gives 6 A usable against the 5 A board cap, and it opens well below
the 7.5 A blade fuse the spec requires upstream.

**There is no sub-milliamp 3.3 V LDO in SOT-223.** Every SOT-223 part is 1117-class with
2-12 mA quiescent. The µA-class parts — HT7833 (2 µA), ME6231A33M3G (1.8 µA) — exist only
in SOT-89 and SOT-23, and SOT-23 cannot dissipate U3's 0.55 W. So the "drop-in low-Iq
replacement" idea does not exist. Taking the best SOT-223 part available instead:
5 mA → 2 mA, sleep current 5.66 mA → ~2.7 mA, roughly 4.1 → 1.9 Ah per month parked.
Getting to µA needs a SOT-89 footprint change (HT7833, thin stock) or a proper 12 V → 3.3 V
converter. **Deferred to Rev B**, along with the 0.55 W dissipation that shares the cause.

**J2's label invited destroying the board.** Its value read "VIN 9-24V" while D5 is an
SMBJ18A — 18 V standoff, ~20 V breakdown. At 24 V the TVS conducts continuously and dies.
The spec doc still says SMBJ33A. This is not a decision to reopen: an SMBJ33A clamps at
~53 V, above the AP63301's 32 V absolute maximum, so a 24 V-capable version of this board
cannot protect its own buck. The parts are right and 12 V-only is correct — the label is a
leftover from the earlier 24 V design. `VANDIMMER-4CH-2ADDR-SPEC-v2.0.md` line 77 needs the
same correction.

**No thermal vias on the DPAK tabs.** Settled by the spec's own numbers, not by preference:
Q1-Q4 dissipate 0.06 W each (2 A × 2 A × 0.015 Ω, ~3 °C rise) and Q5 dissipates 0.75 W into
645 mm² for ~34 °C. Vias would need bottom-layer islands that cut the GND pour, and buy
nothing.

**I2C pull-ups go on the board.** A bus master should provide them, and with none anywhere
the bus silently fails in a way that looks like firmware. Costs two resistors plus routing
near J13.

**The input LC filter gets populated.** A 500 kHz buck plus four PWM channels on an
unfiltered 12 V vehicle rail is the board's biggest conducted-emissions risk, the
footprints already exist, and retrofitting after a failed sweep means a bodge or a respin.
