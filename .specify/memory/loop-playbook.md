# VanDaemon — Build-Controller Loop Playbook

> **What this is.** The operating procedure for the implementation loop (Spec Kit's
> `/plan → /tasks → /implement` flow driven by Claude Code in the repo). It defines *how to command
> the loop*: granularity, where the human gates sit, the exit condition, and how the profile changes
> by feature risk. Standalone (not constitutional) so it's easy to tweak without versioning the
> constitution — but it operates **within** the constitution; where they conflict, the constitution
> wins (esp. §IV safety gates and §VIII loop limits).
>
> Written against Constitution v0.1.0. Status: DRAFT (pending Stuart review).

---

## 1. The loop, in one line

For a given feature: **`/plan` → (human approves plan) → `/tasks` → `/implement` runs to green →
merge on green.** The loop self-fixes build/test failures; the human gates at the plan, not the diff.

## 2. Default autonomy profile — "Trusted-Test" (applies to 0001 and any feature meeting §4)

- **Granularity:** *feature-at-once.* `/implement` runs the whole `/tasks` list for the feature in
  one pass, iterating build/test/fix until green.
- **Human gate:** *plan only.* The human reviews and approves `/plan` + `/tasks` before any code is
  written. After approval the loop runs to green and **may self-merge to the working branch**.
- **Exit condition:** `dotnet build` succeeds **and** `dotnet test` is green against the feature's
  stated acceptance criteria. Green = done. Red = keep iterating (or stop and report per §5).
- **Trigger:** *manual.* The human starts the loop (`/implement`) when ready. No scheduled/background
  runs for foundation work.
- **Post-run:** the loop reports what it built, the test summary, and any acceptance criterion it
  could not satisfy.

## 3. Why this profile is safe *here*

The autonomy is borrowed entirely from the strength of the exit condition. 0001 is pure .NET/Blazor,
fully verifiable off-device by `dotnet test`, with no hardware, root, flash, or brick risk. A green
bar is therefore a real, objective signal — so plan-only gating is justified. **This profile's
validity is contingent on that test-backed exit condition existing.** Remove the objective test and
the justification evaporates (see §4).

## 4. Profile selection — the loop MUST pick its profile by feature risk

Before running, classify the feature and apply the matching profile. When in doubt, pick the stricter.

| Feature class | Objective self-check? | Profile | Gates | Self-merge? |
|---|---|---|---|---|
| **A. .NET/Blazor, test-backed** (e.g. 0001 contracts, services, UI plugins) | Yes — `dotnet test` | **Trusted-Test** (§2) | Plan only | Yes, on green |
| **B. Kotlin/APK, unit-testable parts only** (shell logic, receivers' pure logic) | Partial — builds + unit tests, **no on-device proof** | **Build-Only** | Plan **and** artifact review | No — stops at artifact + checklist |
| **C. On-device / hardware / launcher wiring** (reverse/ACC/wheel-key, WebView behaviour, survival across sleep) | No automated check exists | **Human-Verified** | Plan, artifact, **and on-vehicle verification by Stuart** | No |
| **D. Irreversible** (root, flash, firmware repack, anything brick-risk) | N/A | **Forbidden to loop** | Produced as procedure + checklist **only**; a human executes | No — never |

Rules that follow from the table:
- The loop may **only self-merge** for Class A.
- For Class B/C, "done" is a **tested, installable artifact + a verification checklist** (Constitution
  §VIII.3), never a success claim the loop cannot demonstrate. **No green-washing** (§VIII.5).
- Class D is never executed by the loop or any automation (Constitution §IV.4). The loop's output is
  a documented procedure for a human, with backup + tested-unbrick prerequisites (§IV.1–2) stated.
- A feature that mixes classes is split: the Class-A parts may run Trusted-Test; the Class-B/C/D
  parts drop to their stricter profile. The loop must not let an autonomous Class-A pass smuggle in
  Class-C work.

## 5. Stop-and-report conditions (the loop must halt and surface, not push through)

- Acceptance criteria cannot be met after reasonable iteration → stop, report which and why.
- The work turns out to be a stricter class than planned (e.g. a "service" task actually needs
  on-device behaviour) → stop, reclassify, re-gate.
- A task would require an irreversible action (Class D) → stop immediately; produce procedure, do not
  execute.
- Tests are flaky/failing for environmental reasons → stop, report; do not mark done, do not disable
  tests to force green.

## 6. Human checklist at the plan gate (Class A)

Before approving `/plan` + `/tasks`, confirm: (a) acceptance criteria are genuinely covered by tests;
(b) the feature is truly Class A (no on-device assumption hiding in a task); (c) no task drifts
outside the feature's stated scope; (d) contracts/interfaces match the spec. If all hold, approve and
let it run.

## 7. Evolving the profile

This profile is deliberately conservative-by-class, autonomous-where-safe. Loosen only by moving the
*objective check* earlier (e.g. add on-device smoke tests later that could let some Class-C work earn
a Build-Only-plus profile). Tighten immediately if a green bar ever proves to have meant nothing.

### Change log
- DRAFT — Initial. Default Trusted-Test profile for 0001 (feature-at-once, plan-only gate, self-merge
  on green), with risk-class table downgrading gates automatically for Kotlin/on-device/irreversible
  work.
