<!--
═══════════════════════════════════════════════════════════════════════════════
SYNC IMPACT REPORT
═══════════════════════════════════════════════════════════════════════════════
Version: 2.0.0 → 2.1.0
Change Type: MINOR — merge: both principle sets now binding

Rationale: v2.0.0 had replaced the original IoT principles with the head-unit
extension principles. This amendment re-instates the Core Platform Principles
(I–V) plus their Architecture Requirements and Development Workflow from v1.0.0,
and keeps the Head-Unit Extension principles (renumbered VI–XIII). Both sets are
binding. Re-introduction is additive (nothing removed vs 2.0.0) → MINOR.

Structure (single continuous numbering):
  Part I — Core Platform Principles (VanDaemon IoT system)
    I.   Plugin-First Hardware Abstraction
    II.  Real-Time Reliability (NON-NEGOTIABLE)
    III. Offline-First & Local Storage
    IV.  Test-Driven Hardware Integration
    V.   Clean Architecture & Separation of Concerns
    + Architecture Requirements, + Development Workflow
  Part II — Head-Unit Extension Principles
    VI.   Scope & Intent                  (was II-constitution I)
    VII.  Platform Truths                 (was II)
    VIII. Source Discipline               (was III)
    IX.   Safety & Reversibility          (was IV)
    X.    Launcher & Coexistence Rules    (was V)
    XI.   Native ↔ WASM Boundary          (was VI)
    XII.  Engineering Standards           (was VII)
    XIII. The Implementation Loop & Limits (was VIII)
  Governance
    XIV. Amendment & Governance           (merged old + new)

Cross-reference remap in Part II (old → new):
  II.3→VII.3, II.4→VII.4, Principle IV→IX (IV.1/IV.2→IX.1/IX.2, IV.4→IX.4),
  Principle V→X.

Conflict reconciliation:
  - "Plugin" is dual-defined and reconciled by Principle VI.3 two-tier model:
    server-side hardware plugins (Principle I) vs head-unit UI/WASM plugins.
  - Two distinct "safety" domains coexist: runtime control fail-safe
    (Architecture Requirements) vs firmware/flash reversibility (Principle IX).

Templates Requiring Updates:
  ✅ plan-template.md   - generic constitution reference only; no edit required.
  ✅ spec-template.md   - no hardcoded references; no edit required.
  ✅ tasks-template.md  - no hardcoded references; no edit required.

Follow-up TODOs:
  - None. RATIFICATION_DATE retained as 2025-11-21 (original artifact adoption).
═══════════════════════════════════════════════════════════════════════════════
-->

# VanDaemon Constitution

This constitution governs **both** the core VanDaemon IoT system (the .NET/Blazor/SignalR
camper-van control platform) **and** the VanDaemon Head-Unit Extension (a thin Kotlin launcher
shell hosting the VanDaemon Blazor UI in a WebView on a Teyes FYT head unit). **Both principle
sets below are binding.** Part I applies to all VanDaemon work; Part II adds head-unit-specific
rules. Where a feature spec conflicts with this document, **this document wins**; where Part II
addresses a head-unit concern more specifically than Part I, the more specific rule governs that
work, unless explicitly amended here.

## Part I — Core Platform Principles

### I. Plugin-First Hardware Abstraction

All hardware integrations MUST be implemented through the plugin system. Direct hardware access in application code is **prohibited**.

**Requirements**:
- Every sensor or control device MUST implement `ISensorPlugin` or `IControlPlugin` interfaces
- Plugins MUST be self-contained, independently testable, and documented
- Plugins MUST support graceful degradation (fail-safe behavior when hardware unavailable)
- Configuration MUST be passed via dictionary (JSON-serializable types only)
- Plugin initialization MUST validate hardware connectivity before returning success

**Rationale**: Camper vans vary widely in hardware configurations. Plugin abstraction enables support for Modbus, I2C, Victron Cerbo, and future integrations without changing application code.

### II. Real-Time Reliability (NON-NEGOTIABLE)

Real-time communication latency MUST be < 500ms end-to-end for safety-critical controls (water pump, heater, lighting).

**Requirements**:
- SignalR hub MUST broadcast state changes within 100ms of hardware event
- Background monitoring service MUST refresh tank levels every 5 seconds (configurable minimum 1s)
- WebSocket disconnections MUST trigger automatic reconnection with exponential backoff
- UI MUST display connection status prominently (green badge = connected, red = disconnected)
- Control actions MUST provide visual feedback within 200ms (optimistic UI updates allowed)

**Rationale**: Users rely on real-time feedback when operating critical van systems. Delayed updates could lead to water overflow, battery drain, or safety hazards.

### III. Offline-First & Local Storage

The system MUST function completely offline without internet connectivity. No cloud dependencies in core functionality.

**Requirements**:
- All configuration MUST be stored locally (JSON files or SQLite)
- Real-time data (tank levels, control states) MUST be served from in-memory cache
- Web frontend MUST be served as static files (Blazor WebAssembly)
- API MUST run on local network only (default: Raspberry Pi on van's WiFi)
- Cloud sync features MUST be optional and clearly labeled

**Rationale**: Camper vans frequently operate in remote locations without cellular or internet access. The system must be fully functional offline.

### IV. Test-Driven Hardware Integration

Hardware integration code MUST be testable without physical devices present.

**Requirements**:
- Every plugin MUST have a simulated counterpart for testing (e.g., `SimulatedSensorPlugin`)
- Simulated plugins MUST generate realistic data with gradual changes and noise
- Unit tests MUST use mocked plugins (Moq framework required)
- Integration tests MUST verify plugin contract compliance (initialization, reading, state changes)
- Hardware-specific tests MUST be clearly marked and skippable in CI/CD

**Rationale**: Cannot test Modbus devices, I2C sensors, or Victron integration in CI pipeline. Simulated plugins enable TDD workflow and continuous testing.

### V. Clean Architecture & Separation of Concerns

Project MUST maintain strict layer separation: Core → Application → Infrastructure → API.

**Requirements**:
- **Core layer**: Domain entities only, no dependencies
- **Application layer**: Business logic services, must not reference Infrastructure
- **Infrastructure layer**: Data access, external APIs, persistence implementations
- **API layer**: Controllers, SignalR hubs, dependency injection configuration
- **Plugin layer**: Hardware abstractions, independent of application services
- Controllers MUST be thin wrappers around services (no business logic)
- Services MUST be registered as Singletons (maintain in-memory state)
- All dependencies MUST be injected via constructor

**Rationale**: Clean architecture ensures testability, maintainability, and enables future database migration (JSON → SQLite) without touching application code.

### Architecture Requirements

#### Safety & Fail-Safe Mechanisms

**Critical controls** (water pump, heater, propane) MUST implement fail-safe behavior:
- Hardware plugin connection loss MUST default to safe state (pump OFF, heater OFF)
- Alert service MUST generate critical alerts for hardware failures
- Control state MUST be verified after each command (read-back confirmation)
- Timeout on control operations: 5 seconds maximum, then raise alert

#### Data Persistence Strategy

**Two-tier storage model** (established pattern):
1. **Configuration data**: Persisted to JSON files in `data/` directory (thread-safe via `JsonFileStore`)
2. **Real-time data**: In-memory collections in services (tank levels, control states)

**Migration path**: Infrastructure layer prepared for SQLite implementation. When migrating:
- Configuration data moves to SQLite tables
- Real-time data remains in-memory (performance critical)
- JsonFileStore becomes obsolete (deprecated gracefully)

#### SignalR Communication Pattern

**Group-based subscriptions** MUST be used for targeted broadcasts:
- `tanks` group: Tank level updates
- `controls` group: Control state changes
- `alerts` group: Alert notifications

Clients MUST explicitly subscribe to groups (security boundary). Server-side broadcasts use `IHubContext<TelemetryHub>` from background service.

### Development Workflow

#### Branch Strategy

- `main` branch: Production-ready code, requires PR approval
- Feature branches: `<issue-number>-<feature-name>` (e.g., `42-modbus-plugin`)
- All commits to `main` MUST include tests and pass CI/CD

#### Testing Gates

**Before merging to main**:
- ✅ All unit tests pass (`dotnet test VanDaemon.sln`)
- ✅ No compiler warnings in Release build
- ✅ Code follows project conventions (async/await, nullable reference types)
- ✅ New services have corresponding tests in `tests/` directory
- ✅ Plugin implementations include simulated counterpart for testing

#### Commit Message Format

Follow conventional commits:
- `feat:` New features or plugin implementations
- `fix:` Bug fixes or hardware integration corrections
- `docs:` Documentation updates (README, CLAUDE.md, constitution)
- `test:` Test additions or corrections
- `refactor:` Code restructuring without behavior changes
- `chore:` Dependency updates, build script changes

Include Claude Code attribution:
```
🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>
```

#### Code Quality Standards

**Nullable reference types**: Enabled project-wide (`<Nullable>enable</Nullable>`)
- Use `?` for nullable types explicitly
- Avoid `!` null-forgiving operator except when certainty proven

**Async patterns**:
- All I/O methods MUST be async with `Async` suffix
- Accept `CancellationToken cancellationToken = default` parameter
- Use `await` instead of `.Result` or `.Wait()`

**Logging**:
- Use structured logging: `_logger.LogInformation("Tank {TankId} level: {Level}%", tankId, level)`
- No `Console.WriteLine` in production code
- Log levels: Debug (verbose), Information (state changes), Warning (degraded), Error (failures), Critical (safety issues)

## Part II — Head-Unit Extension Principles

This part governs the head-unit customisation work (Kotlin launcher shell + WASM/Blazor UI plugins
on a Teyes FYT head unit, talking to the existing VanDaemon API).

### VI. Scope & Intent

1. **What this project is.** Customising the *existing* Android install on a Teyes head unit and
   building a companion in-dash UI for VanDaemon. Specifically: a thin native (Kotlin) launcher
   shell that hosts the VanDaemon Blazor UI in a WebView, with an extensible plugin model.
2. **What this project is NOT (current phase).** It is **not** building or flashing a full custom
   ROM. It is **not** van-system electrical integration beyond reading data the VanDaemon API
   already exposes. Keep all findings and code modular so a later integration phase can build on
   them without rework.
3. **Two-tier plugin model is the architectural spine.** *Hardware* plugins
   (sensors/controls: I2C, Modbus, Victron, etc.) live **server-side** in the VanDaemon API/host,
   where they can physically reach the hardware (these are the Principle I plugins). *UI /
   presentation* plugins live **on the head unit** as lazy-loaded Blazor/WASM assemblies rendered
   in the WebView. The two tiers are split by **where the capability physically lives**, never
   merged. No feature may put hardware access inside a WebView-hosted WASM plugin.

### VII. Platform Truths (verified vs anecdotal — treat as binding facts unless re-verified on-device)

These correct common wrong assumptions. An implementing agent MUST NOT re-derive contrary
assumptions from general Android knowledge.

1. **SoC is Unisoc, not Allwinner (VERIFIED).** The Teyes CC3 (incl. 2K / 360) is an **FYT board
   on a Unisoc UIS7862 (ums512)** SoC — Spreadtrum/Unisoc, *not* Allwinner. Tooling is the **FYT
   toolchain** (`ro.fyt.*` properties, `lsec6315update`, `AllAppUpdate.bin`) and **SPD Research
   Download / CM2** for recovery — *not* Allwinner FEL / PhoenixSuit / SP Flash, and *not* MTK SP
   Flash Tool. (The older AC8257-class units are a different, earlier Teyes generation. The X1 may
   be a different platform again and must be confirmed before any X1-specific assumption.)
2. **Exact build must be identified before any build-specific or irreversible step (VERIFIED need).**
   Confirm model + SoC + Android version + build fingerprint via
   `adb shell getprop | grep -iE "fyt|ums512|7862|fingerprint|product"` and record the exact
   firmware package/date. Teyes ships per-variant firmware; several procedures are build-specific.
3. **The stock Teyes launcher owns critical vehicle functions (VERIFIED).** Vehicle settings, EQ/DSP,
   reverse-camera, handbrake/ACC triggers and steering-wheel buttons are bound to the stock
   launcher and its services. Therefore: **never remove the stock Teyes app.** A replacement
   launcher coexists with it and forwards to its activities/intents; it does not replace its
   functions. (See Principle X.)
4. **Aggressive OEM task-killer (VERIFIED).** These units kill background apps via a custom OEM
   killer (beyond stock Doze). Survival across sleep/wake and ACC-off requires the app's package in
   the **autostart list** and in **`skipkillapp.prop`** (e.g. `com.example.app = -15`). On FYT,
   `skipkillapp.prop` only initialises from a **full `AllAppUpdate.bin` flashed to the unit** —
   it cannot simply be dropped in. The launcher itself, and any UI process that must persist, are
   subject to this.
5. **Old WebView / browser engine is a live risk (ANECDOTAL — must be verified on-device).** The
   stock browser/WebView may be too old to run modern Blazor WASM. Confirm with
   `adb shell dumpsys package com.google.android.webview | grep versionName` before relying on it.
   Mitigations (in preference order): target a current WebView, or AOT-compile + brotli-precompress
   the Blazor app, or host Blazor components natively. Treat WebView capability as an unproven
   assumption until tested.
6. **Bricking is a real, repeatedly-reported risk (ANECDOTAL but credible).** Root and firmware
   repack on these units have caused bricks in community reports. No irreversible step proceeds
   without Principle IX satisfied.

### VIII. Source Discipline

1. Any factual claim about the platform, firmware, or a procedure MUST cite its community source
   (XDA, Teyes official firmware pages, 4PDA/Telegram) and MUST be tagged **VERIFIED** (corroborated
   across sources or the official Teyes firmware listing) or **ANECDOTAL** (single report / forum
   claim). Do not silently promote anecdotal to verified.
2. Primary community references for this platform:
   - XDA — *General FYT-based Unisoc UIS7862 (Q&A, mods, firmware)* mega-thread.
   - XDA — *TEYES CC3* hardware-development thread; *Teyes CC3 Root Tutorial*; *Teyes CC3 2K Open-Source Firmware*.
   - XDA — *GUIDE: FYT 7870 generic head unit unbrick procedures*; *Modding Joying/FYT without root*.
   - Teyes official firmware pages (per-variant, e.g. CC3 2K/360 dated packages).
3. Prefer the official Teyes firmware page for the unit's exact variant as the stock-image source of record.

### IX. Safety & Reversibility (hard gates — non-negotiable)

1. **Backup before anything irreversible.** Before any root, flash, or firmware repack: obtain the
   exact matching stock firmware/PAC for the confirmed build, and take a full partition/backup.
2. **Tested unbrick route required.** A working recovery path (SPD Research Download with the correct
   PAC, drivers installed; CM2 as fallback; UART-pin method understood for severe bricks) MUST be
   confirmed *and understood* before the first irreversible action — not improvised after a brick.
3. **Reversible-first ordering.** Always prefer the unrooted, reversible approach (launcher via
   `ro.fyt.launcher`, autostart list, etc.) before considering root. Root is a last resort, only
   when a reversible path cannot meet the requirement, and only with IX.1 and IX.2 satisfied.
4. **No autonomous irreversible actions.** No agent or automated loop may perform a flash, root, or
   anything that risks a brick. Such steps are produced as a documented procedure + checklist for a
   human (Stuart) to execute and verify on the vehicle.

### X. Launcher & Coexistence Rules

1. The launcher is an ordinary Android app that declares the `HOME` + `DEFAULT` intent categories;
   no root is required to install or set it as default.
2. The launcher **coexists** with the stock Teyes app (Principle VII.3). It forwards to stock
   activities/intents for vehicle settings, DSP/EQ, reverse-cam, etc. — discovered per-unit via
   `dumpsys`/`pm` — rather than reimplementing or removing them.
3. Hardware/vehicle event wiring (reverse trigger, ACC on/off, steering-wheel keycodes) is
   FYT-specific, only partly documented, and **must be reverse-engineered and verified on the
   physical unit**. Specs treat each such binding as *needs-hardware-verification* until proven.
4. The launcher (and any persistent UI process) MUST be registered for autostart and in
   `skipkillapp.prop` (Principle VII.4), or it will be culled on sleep/ACC-off.

### XI. Native ↔ WASM Boundary

1. The Kotlin shell exposes a **small, explicit native capability bridge** to the WebView
   (e.g. reverse-cam state, ACC state, wheel-key events, "open Teyes DSP"). This bridge is the
   *only* way WASM/Blazor UI plugins reach native/vehicle capabilities.
2. WASM/Blazor UI plugins are sandboxed to the web layer: they render UI and call the VanDaemon
   API and the bridge. They do **not** get direct native, filesystem, or hardware access.
3. Every native capability exposed on the bridge MUST be documented as **confirmed-reachable** or
   **needs-reverse-engineering**, so downstream specs know what is real vs aspirational.
4. The bridge surface is a **contract**: changes to it are spec-level changes, not incidental
   implementation details.

### XII. Engineering Standards (inherits the repo CLAUDE.md)

1. Server-side / .NET work follows the existing VanDaemon conventions and Part I principles: .NET 10,
   Clean Architecture layering, services as singletons holding in-memory state, `Async` suffix on
   async methods, `CancellationToken` on async APIs, structured logging with message templates,
   nullable reference types enabled, `Dictionary<string, object>` (JSON-serialisable) for plugin
   configuration.
2. New hardware plugins implement the existing `ISensorPlugin` / `IControlPlugin` abstractions and
   register as singletons; they are initialised after `app.Build()`. (See Principle I and repo CLAUDE.md.)
3. Tests use xUnit + FluentAssertions + Moq, Arrange/Act/Assert. **Every feature spec defines its
   acceptance criteria as testable statements**; where the .NET/web layer can prove them, the
   implementation loop's exit condition is `dotnet test` green.
4. Kotlin/Android work: the shell stays **thin** — home-screen shell, WebView host, hardware
   receivers, app-launch tiles, and the native bridge. Application logic stays in C#/Blazor. The
   Kotlin layer should remain on the order of a few hundred lines.

### XIII. The Implementation Loop & Its Limits

1. **Three loops, distinct.** (a) Design loop — humans + design assistant produce specs. (b)
   Implementation loop — Claude Code in the repo plans → builds → tests → iterates to green. (c)
   Release/on-hardware loop — deploy to the unit and verify on the vehicle.
2. **The implementation loop is automatable only where an objective test exists.** For .NET/web
   work, `dotnet build` + `dotnet test` provide that objective exit condition. For Kotlin/APK work,
   the loop may build and unit-test but **cannot** self-verify on-device behaviour.
3. **The on-hardware loop is human-gated by design** (Principle IX.4). The implementation loop's
   deliverable for any hardware/launcher item is a **tested, installable artifact plus a
   verification checklist** for Stuart — never a claim of success it cannot demonstrate.
4. **Specs must be self-contained.** The implementing agent does not have the design conversation —
   only the repo files. Each spec carries its own context, constraints, interface contracts,
   acceptance criteria, and an explicit *out-of-scope / needs-hardware* section.
5. **No green-washing.** An item is "done" only when its stated acceptance criteria are met. Items
   that cannot be verified without hardware are marked *blocked-on-hardware*, not *done*.

## Governance

### XIV. Amendment & Governance

**Constitution authority.** This constitution **supersedes all other practices**. When conflicts
arise between this document and code comments, READMEs, or verbal agreements, the constitution takes
precedence. Both Part I and Part II are binding (see preamble for precedence on head-unit-specific
concerns).

**Amendment process.**
1. Propose the amendment (GitHub issue with the `constitution` label, or design-session draft under
   `.spec-drafts/`) with rationale, impact analysis, and migration plan.
2. Bump the version per semantic versioning:
   - **MAJOR**: backward-incompatible governance/principle removals or redefinitions.
   - **MINOR**: a new principle/section added, or materially expanded guidance.
   - **PATCH**: clarifications, wording improvements, typo fixes, non-semantic refinements.
3. Update the Sync Impact Report at the top of this file.
4. Propagate changes to affected templates (`.specify/templates/*`) and documentation.
5. Obtain approval from the repository owner (Stuart) before the amendment is binding.

**Versioned references.** Feature specs reference the constitution version they were written
against. Principle VII (Platform Truths) entries may move from ANECDOTAL to VERIFIED only with a
cited, corroborating on-device or multi-source confirmation, recorded in the change log.

**Compliance review.**
- All PRs MUST verify compliance with the Core Principles (I–V) and, for head-unit work, the
  Head-Unit Extension principles (VI–XIII); any exception MUST be justified with a safety, scope, or
  performance rationale, referencing the relevant constitution section.
- **Quarterly review** of constitution adherence: check for drift between the constitution and the
  actual codebase patterns; update via the amendment process if established patterns prove superior.

**Runtime guidance.** For day-to-day development guidance beyond constitutional principles, refer to:
- `CLAUDE.md` — Architecture patterns, build commands, common gotchas
- `README.md` — Quick start, deployment, troubleshooting
- `PROJECT_PLAN.md` — Feature roadmap and phased implementation

### Change log

- **2.1.0 (2026-06-13)** — Merged: re-instated the Core Platform Principles (I–V) plus Architecture
  Requirements and Development Workflow from v1.0.0 alongside the Head-Unit Extension principles
  (renumbered VI–XIII). Both sets binding. Updated internal cross-references and unified governance.
- **2.0.0 (2026-06-13)** — Replaced the generic VanDaemon IoT constitution with the Head-Unit
  Extension constitution: FYT/Unisoc platform correction, two-tier plugin model, hybrid
  WASM-in-Blazor + native bridge architecture, safety/reversibility gates, and the three-loop
  process. (Superseded by 2.1.0, which restores the IoT principles.)
- **1.0.0 (2025-11-21)** — Initial VanDaemon IoT constitution (plugin-first abstraction, real-time
  reliability, offline-first, test-driven hardware integration, clean architecture).
- **0.1.0** — Initial head-unit design-session draft (`.spec-drafts/constitution.md`), pending review.

**Version**: 2.1.0 | **Ratified**: 2025-11-21 | **Last Amended**: 2026-06-13
