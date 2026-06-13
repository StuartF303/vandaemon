# VanDaemon Head-Unit Extension — Project Constitution

> **Purpose of this file.** This is the durable, non-negotiable set of principles for the
> VanDaemon head-unit customisation work (Kotlin launcher shell + WASM/Blazor UI plugins on a
> Teyes FYT head unit, talking to the existing VanDaemon API). It is the input for Spec Kit's
> `/constitution` command. Every `/specify`, `/plan`, and `/implement` step inherits these rules.
> Where a feature spec conflicts with this document, **this document wins** unless explicitly
> amended here.
>
> Status: DRAFT (pending review by Stuart). Version: 0.1.0.

---

## I. Scope & Intent

1. **What this project is.** Customising the *existing* Android install on a Teyes head unit and
   building a companion in-dash UI for VanDaemon. Specifically: a thin native (Kotlin) launcher
   shell that hosts the VanDaemon Blazor UI in a WebView, with an extensible plugin model.
2. **What this project is NOT (current phase).** It is **not** building or flashing a full custom
   ROM. It is **not** van-system electrical integration beyond reading data the VanDaemon API
   already exposes. Keep all findings and code modular so a later integration phase can build on
   them without rework.
3. **Two-tier plugin model is the architectural spine.** *Hardware* plugins
   (sensors/controls: I2C, Modbus, Victron, etc.) live **server-side** in the VanDaemon API/host,
   where they can physically reach the hardware. *UI / presentation* plugins live **on the head
   unit** as lazy-loaded Blazor/WASM assemblies rendered in the WebView. The two tiers are split
   by **where the capability physically lives**, never merged. No feature may put hardware access
   inside a WebView-hosted WASM plugin.

## II. Platform Truths (verified vs anecdotal — treat as binding facts unless re-verified on-device)

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
   functions. (See Principle V.)
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
   without Principle IV satisfied.

## III. Source Discipline

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

## IV. Safety & Reversibility (hard gates — non-negotiable)

1. **Backup before anything irreversible.** Before any root, flash, or firmware repack: obtain the
   exact matching stock firmware/PAC for the confirmed build, and take a full partition/backup.
2. **Tested unbrick route required.** A working recovery path (SPD Research Download with the correct
   PAC, drivers installed; CM2 as fallback; UART-pin method understood for severe bricks) MUST be
   confirmed *and understood* before the first irreversible action — not improvised after a brick.
3. **Reversible-first ordering.** Always prefer the unrooted, reversible approach (launcher via
   `ro.fyt.launcher`, autostart list, etc.) before considering root. Root is a last resort, only
   when a reversible path cannot meet the requirement, and only with IV.1 and IV.2 satisfied.
4. **No autonomous irreversible actions.** No agent or automated loop may perform a flash, root, or
   anything that risks a brick. Such steps are produced as a documented procedure + checklist for a
   human (Stuart) to execute and verify on the vehicle.

## V. Launcher & Coexistence Rules

1. The launcher is an ordinary Android app that declares the `HOME` + `DEFAULT` intent categories;
   no root is required to install or set it as default.
2. The launcher **coexists** with the stock Teyes app (Principle II.3). It forwards to stock
   activities/intents for vehicle settings, DSP/EQ, reverse-cam, etc. — discovered per-unit via
   `dumpsys`/`pm` — rather than reimplementing or removing them.
3. Hardware/vehicle event wiring (reverse trigger, ACC on/off, steering-wheel keycodes) is
   FYT-specific, only partly documented, and **must be reverse-engineered and verified on the
   physical unit**. Specs treat each such binding as *needs-hardware-verification* until proven.
4. The launcher (and any persistent UI process) MUST be registered for autostart and in
   `skipkillapp.prop` (Principle II.4), or it will be culled on sleep/ACC-off.

## VI. Native ↔ WASM Boundary

1. The Kotlin shell exposes a **small, explicit native capability bridge** to the WebView
   (e.g. reverse-cam state, ACC state, wheel-key events, "open Teyes DSP"). This bridge is the
   *only* way WASM/Blazor UI plugins reach native/vehicle capabilities.
2. WASM/Blazor UI plugins are sandboxed to the web layer: they render UI and call the VanDaemon
   API and the bridge. They do **not** get direct native, filesystem, or hardware access.
3. Every native capability exposed on the bridge MUST be documented as **confirmed-reachable** or
   **needs-reverse-engineering**, so downstream specs know what is real vs aspirational.
4. The bridge surface is a **contract**: changes to it are spec-level changes, not incidental
   implementation details.

## VII. Engineering Standards (inherits the repo CLAUDE.md)

1. Server-side / .NET work follows the existing VanDaemon conventions: .NET 10, Clean Architecture
   layering, services as singletons holding in-memory state, `Async` suffix on async methods,
   `CancellationToken` on async APIs, structured logging with message templates, nullable reference
   types enabled, `Dictionary<string, object>` (JSON-serialisable) for plugin configuration.
2. New hardware plugins implement the existing `ISensorPlugin` / `IControlPlugin` abstractions and
   register as singletons; they are initialised after `app.Build()`. (See repo CLAUDE.md.)
3. Tests use xUnit + FluentAssertions + Moq, Arrange/Act/Assert. **Every feature spec defines its
   acceptance criteria as testable statements**; where the .NET/web layer can prove them, the
   implementation loop's exit condition is `dotnet test` green.
4. Kotlin/Android work: the shell stays **thin** — home-screen shell, WebView host, hardware
   receivers, app-launch tiles, and the native bridge. Application logic stays in C#/Blazor. The
   Kotlin layer should remain on the order of a few hundred lines.

## VIII. The Implementation Loop & Its Limits

1. **Three loops, distinct.** (a) Design loop — humans + design assistant produce specs. (b)
   Implementation loop — Claude Code in the repo plans → builds → tests → iterates to green. (c)
   Release/on-hardware loop — deploy to the unit and verify on the vehicle.
2. **The implementation loop is automatable only where an objective test exists.** For .NET/web
   work, `dotnet build` + `dotnet test` provide that objective exit condition. For Kotlin/APK work,
   the loop may build and unit-test but **cannot** self-verify on-device behaviour.
3. **The on-hardware loop is human-gated by design** (Principle IV.4). The implementation loop's
   deliverable for any hardware/launcher item is a **tested, installable artifact plus a
   verification checklist** for Stuart — never a claim of success it cannot demonstrate.
4. **Specs must be self-contained.** The implementing agent does not have the design conversation —
   only the repo files. Each spec carries its own context, constraints, interface contracts,
   acceptance criteria, and an explicit *out-of-scope / needs-hardware* section.
5. **No green-washing.** An item is "done" only when its stated acceptance criteria are met. Items
   that cannot be verified without hardware are marked *blocked-on-hardware*, not *done*.

## IX. Amendment

1. This constitution is versioned (semver). Material changes bump the version and are recorded
   below. Feature specs reference the constitution version they were written against.
2. Principles II (Platform Truths) entries may move from ANECDOTAL to VERIFIED only with a cited,
   corroborating on-device or multi-source confirmation, recorded in the change log.

### Change log
- 0.1.0 — Initial draft from design session (FYT/Unisoc platform correction, two-tier plugin model,
  hybrid WASM-in-Blazor + native bridge architecture, safety gates, three-loop process).
