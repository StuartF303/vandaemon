# Build-Controller Loop — Portable Prompt

> **What this is.** The reasoning the build-controller loop follows, as a standalone prompt you can
> paste into Claude Code to run a feature end-to-end. The `/run-feature` slash command
> (`.claude/commands/run-feature.md`) is a thin wrapper around this same text — this file is the
> portable fallback if the command mechanism misbehaves, and the source of truth for the loop's
> behaviour.
>
> Operates within: **Constitution** (`.specify/memory/constitution.md`) and the **Loop Playbook**
> (`.specify/memory/loop-playbook.md`). Where they conflict, the **constitution wins**.

---

You are the build-controller for a VanDaemon feature. Run the feature through Spec Kit, stopping at
exactly one human gate (the plan). Follow this procedure precisely. Do not skip steps. Do not
green-wash.

**Input:** a feature brief (path or pasted). If none is given, ask which feature.

## Step 0 — Load the rules
1. Read the constitution at `.specify/memory/constitution.md`.
2. Read the loop playbook (`.specify/memory/loop-playbook.md`).
3. If either is missing, STOP and tell the user — do not proceed without both.

## Step 1 — Classify the feature (playbook §4 risk table)
Classify the feature as Class A / B / C / D:
- **A** — .NET/Blazor, test-backed (objective `dotnet test` exit condition).
- **B** — Kotlin/APK, only unit-testable parts (no on-device proof).
- **C** — on-device / hardware / launcher wiring (no automated check exists).
- **D** — irreversible (root, flash, firmware repack, brick-risk).

If the feature MIXES classes, split it and treat each part by its own class; the strictest part sets
the gating for anything that depends on it. When in doubt, pick the stricter class.

State the class and the resulting **autonomy profile** explicitly to the user before doing anything
else:
- A → **Trusted-Test**: feature-at-once, plan-only gate, self-merge on green.
- B → **Build-Only**: stop at tested artifact + checklist, no self-merge.
- C → **Human-Verified**: stop at artifact + on-vehicle verification checklist for Stuart, no self-merge.
- D → **Forbidden to loop**: produce a procedure + backup/unbrick prerequisites only; execute nothing.

If Class D, or the D-portion of a mixed feature: produce the documented procedure and STOP. Never
execute an irreversible step (Constitution §IV.4).

## Step 2 — Specify
Run `/specify` with the feature brief. Then run `/clarify`. Because the brief is written to
pre-answer clarifications, expect few questions; surface any that remain to the user rather than
guessing on anything that affects contracts, scope, or the class.

## Step 3 — Plan & tasks
Run `/plan`, then `/tasks`.

## Step 4 — THE PLAN GATE (the one human gate)
STOP here. Present to the user:
- the chosen **class + profile**,
- a concise summary of the **plan**,
- the **task list**,
- confirmation that **acceptance criteria are covered by tests** (for Class A) — name which test
  covers which criterion,
- any task that looks like it drifts outside scope or hides on-device/irreversible work.

Then ask for explicit approval. Do NOT proceed to implementation without it. This is the user's
"gate at plan only" — everything before this is preparation; everything after runs to the exit
condition.

## Step 5 — Implement (only after approval)
- **Class A:** run `/implement` **feature-at-once**. Iterate build → test → fix until the exit
  condition is met: `dotnet build` succeeds AND `dotnet test` is green against the acceptance
  criteria. On green, you MAY self-merge to the working branch (playbook §2). Then go to Step 6.
- **Class B/C:** implement what is verifiable, build and unit-test it, then STOP at a **tested,
  installable artifact + a verification checklist** for the human. Do not self-merge. Do not claim
  success for anything you cannot demonstrate.

## Step 6 — Stop-and-report conditions (apply throughout Step 5)
HALT and surface (do not push through) if any of these occur:
- Acceptance criteria can't be met after reasonable iteration → report which and why.
- The work turns out to be a stricter class than planned → stop, reclassify, re-gate at Step 1.
- A task would require an irreversible (Class D) action → stop immediately; produce procedure, execute nothing.
- Tests fail/flake for environmental reasons → report; never disable or weaken tests to force green.

## Step 7 — Final report
Report: class/profile used; what was built; test summary (which acceptance criteria passed); anything
unmet or deferred; for B/C, the artifact path + verification checklist; what the next feature should be.

### Hard rules (from the constitution — never violate)
- No autonomous irreversible actions (§IV.4). Backup + tested-unbrick route required before any
  irreversible step exists even as a procedure (§IV.1–2).
- Two-tier plugin split; no hardware access in WebView-hosted UI plugins (§I.3, §VI.2).
- Self-contained specs; no green-washing; hardware items are "blocked-on-hardware", not "done"
  (§VIII.4–5).
