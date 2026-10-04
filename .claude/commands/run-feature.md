---
description: Run a VanDaemon feature through the Spec Kit loop with playbook-based risk classification, stopping at the plan gate for approval before implementing.
---

# /run-feature

You are the build-controller for a VanDaemon feature. Drive it through Spec Kit
(`/specify → /clarify → /plan → /tasks → /implement`), stopping at exactly one human gate (the
plan). Apply the risk classification and gating from the loop playbook. Operate within the
constitution; where they conflict, the constitution wins. Do not green-wash; do not perform
irreversible actions.

**Argument:** `$ARGUMENTS` — a path to the feature brief (e.g. `.spec-drafts/0001-plugin-architecture.brief.md`)
or a pasted brief. If empty, ask which feature to run.

## Procedure

### 0. Load the rules
- Read `.specify/memory/constitution.md`.
- Read the loop playbook (`.specify/memory/loop-playbook.md`).
- If either is missing, STOP and tell the user. Do not proceed without both.

### 1. Classify (playbook §4 risk table) and announce the profile
Classify the feature A/B/C/D. Mixed features are split; the strictest part sets gating; when in
doubt pick the stricter class. State the class and profile before doing anything:
- **A — Trusted-Test:** feature-at-once, plan-only gate, self-merge on green (objective `dotnet test`).
- **B — Build-Only:** stop at tested artifact + checklist; no self-merge.
- **C — Human-Verified:** stop at artifact + on-vehicle verification checklist for Stuart; no self-merge.
- **D — Forbidden to loop:** produce a procedure + backup/unbrick prerequisites only; execute nothing, then STOP.

### 2. Specify
Run `/specify` with the brief, then `/clarify`. Briefs are pre-loaded to minimise clarifications;
surface any remaining question that affects contracts, scope, or class rather than guessing.

### 3. Plan & tasks
Run `/plan`, then `/tasks`.

### 4. PLAN GATE — stop and request approval
Present: chosen class + profile; a concise plan summary; the task list; for Class A, which test
covers which acceptance criterion; and any task that drifts outside scope or hides on-device /
irreversible work. Ask for explicit approval. Do NOT implement without it.

### 5. Implement (only after approval)
- **Class A:** `/implement` feature-at-once; iterate build→test→fix until `dotnet build` succeeds and
  `dotnet test` is green against the acceptance criteria; MAY self-merge on green.
- **Class B/C:** implement the verifiable part, build + unit-test, then STOP at a tested installable
  artifact + a verification checklist. No self-merge. Claim nothing you cannot demonstrate.

### 6. Stop-and-report (throughout step 5)
HALT and surface if: acceptance criteria can't be met after reasonable iteration; the work proves a
stricter class than planned (reclassify, re-gate); a task needs an irreversible (Class D) action
(stop, produce procedure, execute nothing); or tests fail/flake environmentally (report; never
weaken tests to force green).

### 7. Final report
Class/profile used; what was built; test summary (criteria passed); anything unmet/deferred; for
B/C the artifact path + verification checklist; recommended next feature.

## Hard rules (constitution — never violate)
- No autonomous irreversible actions (§IV.4); backup + tested-unbrick route required before any
  irreversible procedure exists (§IV.1–2).
- Two-tier plugin split; no hardware access in WebView-hosted UI plugins (§I.3, §VI.2).
- Self-contained specs; no green-washing; hardware items are "blocked-on-hardware", not "done" (§VIII.4–5).
