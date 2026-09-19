"""Rev B silk pass -- run INSIDE KiCad's PCB editor scripting console.

Konnect can add board text but cannot edit or delete it, so this finishes what it
started. Run it once after F8 (Update PCB from Schematic), then save:

    exec(open(r"C:/Projects/vandaemon/hw/VDDimmer/tools/revb_silk_pass.py").read())

What it does, all by UUID so nothing else on the board can be touched:
  1. Deletes the eight 0.8 mm J3-J6 polarity marks. They carried Rev B's first pin
     order (pin 1 = LED-), which was reversed on 2026-09-19 once the Molex 22-05-3021
     was test-fitted (REV-B-CHECKLIST.md 4.1.1).
  2. Thickens the eight replacement 1.5 mm marks to a 0.3 mm stroke. Konnect writes
     size/10, and 0.15 mm is not what "obvious" means.
  3. Deletes the "Rev A" text, which is wrong on this board.
  4. Moves the "VANDIMMER-4CH+2A / v 1.0" title off the relocated buck cluster into
     the clear area verified during placement.

Idempotent: a second run finds nothing to delete and reports it.
"""
import pcbnew

OLD_MARKS = [
    "866a112c-0121-4697-a0e4-216a7e94ec4c", "09a79268-ca7d-4902-a46f-8b7add71cc23",
    "5e46ed7c-af5f-4aec-a15b-5cc19fa883cc", "19b95516-cd58-4ccb-ac5f-8933210ca039",
    "68093638-0028-4d90-8493-78accfaa427a", "5537d99f-3216-48da-9966-f0a4fa300f7d",
    "981ba743-a6c9-4a15-9149-9f9c7e843614", "83c6a5de-defb-4e86-9827-aef33292a114",
]
NEW_MARKS = [
    "e6678523-de42-4772-b16c-249f5cb72ee8", "ed754566-f8bf-4d9c-bfbb-608b89c1f9d5",
    "815c4856-f81f-4ccf-ad1c-a41c39a9ee2e", "ad06fb99-3478-499f-a8f0-8f348a558bd5",
    "d520bb5e-9b7f-466f-a0c6-e32293eb29bf", "7dfca23a-489c-40e2-8432-853d17b2c4d4",
    "6e545e3c-92a8-40bb-92a3-932ed58d970b", "6f93fa84-3463-4d7a-8fc5-4abaa246fef0",
]
REV_A = "1770733b-4cd9-4f47-89be-464863ca410a"
TITLE = "7b34da94-8ff2-497d-8981-9a034ffcca95"
TITLE_AT = (112.3, 98.8)
MARK_STROKE_MM = 0.3


def _run(board):
    texts = {d.m_Uuid.AsString(): d for d in board.GetDrawings() if d.GetClass() == "PCB_TEXT"}
    log = []

    for u in OLD_MARKS + [REV_A]:
        d = texts.get(u)
        if d is None:
            log.append("already gone  %s" % u)
            continue
        log.append("deleted       %r at x %.2f" % (d.GetText(), pcbnew.ToMM(d.GetPosition().x)))
        board.Remove(d)

    for u in NEW_MARKS:
        d = texts.get(u)
        if d is None:
            log.append("MISSING new mark %s -- stop and investigate" % u)
            continue
        d.SetTextThickness(pcbnew.FromMM(MARK_STROKE_MM))
        log.append("stroke %.2f   %r at x %.2f" % (MARK_STROKE_MM, d.GetText(),
                                                    pcbnew.ToMM(d.GetPosition().x)))

    d = texts.get(TITLE)
    if d is None:
        log.append("MISSING title text -- stop and investigate")
    else:
        d.SetPosition(pcbnew.VECTOR2I(pcbnew.FromMM(TITLE_AT[0]), pcbnew.FromMM(TITLE_AT[1])))
        log.append("title moved to %s" % (TITLE_AT,))
    return log


if __name__ == "__main__" and "board_path" in globals():
    # Test harness: run against a copy outside KiCad.
    _b = pcbnew.LoadBoard(board_path)
    print("\n".join(_run(_b)))
    _b.Save(board_path)
else:
    print("\n".join(_run(pcbnew.GetBoard())))
    pcbnew.Refresh()
    print("Now save the board (Ctrl+S).")
