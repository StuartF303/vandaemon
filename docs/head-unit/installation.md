# VanDaemon Head-Unit — Installation Guide

> **Status:** Basic user guide (v0.1, first-pass launcher shell). This is the entry-level
> install walkthrough. Debugging/troubleshooting sections are intentionally thin and will be
> expanded as we test on real hardware. Sections marked _(to be expanded)_ are placeholders.
>
> Applies to feature **005-launcher-shell** — the first-pass Kotlin shell that hosts the
> VanDaemon Blazor/WASM UI in a WebView. Governed by Constitution v2.1.0 (Part II).

---

## 1. What you're installing

`vandaemon-shell-debug.apk` — an **ordinary Android app** that opens the VanDaemon dashboard
(the Blazor/WASM UI) full-screen in a WebView. The UI is bundled inside the app, so it runs
**fully offline** with no server on the unit.

**This first pass is not the launcher.** It does not replace your home screen, does not touch
the stock Teyes launcher, and does not read any vehicle signals yet (reverse cam, ACC,
steering-wheel keys are stubbed). It exists to prove the app installs and the UI renders on
the unit.

## 2. Do I need to root the head unit?

**No.** This is a normal sideloaded app. Root is **not** required to install it, run it, or
remove it. Installing it changes nothing about the stock system and is fully reversible
(uninstall removes it cleanly).

> Root only becomes relevant for *later, deferred* milestones (making VanDaemon the default
> launcher, surviving the OEM task-killer across sleep/ACC-off). Even then it's a last resort,
> not a requirement — and none of that is part of this guide.

## 3. Prerequisites

**On the head unit:**
- A Teyes **FYT / Unisoc (UMS512 / ums9230-class)** unit. Confirm yours before relying on any
  unit-specific step (see §7).
- "Install unknown apps" permission available for whatever installs the APK (file manager, or
  ADB). Where this toggle lives varies by unit.

**On your PC (only if building the APK yourself or using ADB):**
- The prebuilt `vandaemon-shell-debug.apk`, **or** the toolchain to build it (see §4).
- `adb` (Android Platform-Tools) if you install over USB/network.

## 4. Getting the APK

Use a prebuilt APK if you have one. To build it from source:

```powershell
# From the repo root. Two-step build (the .NET publish is separate from the Android build).
pwsh build/publish-wasm-to-assets.ps1      # publishes the Blazor UI and stages it into the app
cd app
./gradlew assembleDebug                      # builds the APK
# -> app/build/outputs/apk/debug/vandaemon-shell-debug.apk
```

> **Important — rebuilding after UI changes:** the two steps are deliberately separate. If you
> change the Blazor UI (`src/Frontend/VanDaemon.Web`), you must re-run
> `build/publish-wasm-to-assets.ps1` **before** `./gradlew assembleDebug`, or the APK will ship
> the previously-staged UI. Running `assembleDebug` alone only repackages whatever is already
> staged. (Build toolchain details: see [`app/README.md`](../../app/README.md).)

## 5. Installing on the unit

Pick whichever route fits how you can reach the unit. The APK is debug-signed, so it installs
without Play Store / Google signing — you just need "unknown sources" allowed.

### Route A — ADB over the network (usually easiest)

Most FYT units run ADB over TCP and have no convenient USB-data port. Find the unit's IP in its
WiFi settings, then from your PC:

```bash
adb connect <unit-ip>:5555
adb devices                                   # confirm the unit shows as "device"
adb install vandaemon-shell-debug.apk
adb shell am start -n com.vandaemon.shell/.MainActivity   # launch it
```

### Route B — ADB over USB

Plug your PC into the unit's data USB port, then:

```bash
adb devices                                   # confirm the unit appears
adb install vandaemon-shell-debug.apk
```

### Route C — File-manager sideload (no PC)

1. Copy `vandaemon-shell-debug.apk` to a USB stick or SD card.
2. Insert it into the unit and open the APK with the unit's built-in file manager.
3. Accept the install prompt (enable *Install unknown apps* for the file manager if asked).

## 6. First launch — what you should see

Open **VanDaemon** from the app list. Expected: the VanDaemon dashboard renders — the van
diagram and the bottom navigation (Dashboard / Electrical / Devices / Settings / Fullscreen).

- It is normal for it to show an **offline / "can't load settings"** state: there is no
  VanDaemon backend running on the unit in this first pass. The UI still renders.
- It launches as an **ordinary app**, not as the home screen. That is by design.

If the screen is blank or shows "An unhandled error has occurred", the unit's WebView may be
too old to run the app — see §8.

## 7. Confirm your exact unit (do this once)

Several steps are build-specific. Record your unit's identity before relying on unit-specific
behaviour:

```bash
adb shell getprop | grep -iE "fyt|ums512|7862|fingerprint|product"
adb shell dumpsys package com.google.android.webview | grep versionName
```

Keep the output — the WebView version in particular decides whether the UI will render (§8).

## 8. Verifying it works (on-hardware checklist)

This first pass is delivered with an on-hardware verification checklist that a human runs on the
unit (it cannot be proven off the vehicle). The authoritative list lives in the spec:
[`specs/005-launcher-shell/spec.md`](../../specs/005-launcher-shell/spec.md) → *On-Hardware
Verification Checklist*. In short:

- [ ] Record model / SoC / Android / fingerprint and stock WebView version (§7).
- [ ] APK installs and launches as an ordinary app (not the home screen).
- [ ] The WebView **renders** the VanDaemon UI on the unit.
- [ ] A native bridge call from the UI round-trips through the unit's WebView.
- [ ] The stock Teyes launcher / vehicle settings are untouched by installing or running the app.

## 9. Troubleshooting _(basic — to be expanded as we test on hardware)_

**The app installs but the screen is blank / shows an error.**
The most likely cause is an **outdated stock WebView** that can't run modern WebAssembly. Capture
the diagnostics — the shell logs WebView console and load errors:

```bash
adb logcat -s VanDaemonShell:*
```

Note the WebView `versionName` from §7 and any console errors, and report them. If the WebView is
too old, the fix is a separate (not-yet-built) build path that pre-compiles the UI; the logs are
what tell us whether that's needed.

**`adb` doesn't see the unit.**
ADB access on FYT units is enabled per-variant and is only partly documented — the menu to turn on
developer options / USB debugging differs by build. _(Unit-specific steps to be added here as we
confirm them on the actual hardware.)_

**"Install blocked" / unknown sources.**
Allow installation from the source you're using (file manager or ADB). The exact toggle location
varies by unit. _(To be expanded.)_

**More diagnostics.** _(This section will grow as we debug on real units — logcat filters, common
WebView versions, MIME/asset issues, etc.)_

## 10. Updating

Reinstall the newer APK over the top:

```bash
adb install -r vandaemon-shell-debug.apk      # -r keeps app data, replaces the app
```

(Or reinstall via the file manager — Android replaces the existing app.)

## 11. Uninstalling

```bash
adb uninstall com.vandaemon.shell
```

Or long-press the app icon → Uninstall. This removes the app completely and leaves the stock
system unchanged.

## 12. What's intentionally not here yet (deferred)

- Becoming the default launcher / home screen.
- Reading real vehicle signals (reverse cam, ACC, steering-wheel keys, "open DSP").
- Surviving the OEM task-killer across sleep / ACC-off (autostart, `skipkillapp.prop`).
- A pre-compiled (AOT) build for older WebViews — only if §8 shows the stock WebView can't run
  the standard build.

These are later milestones, each gated on on-hardware findings from this first pass.

---

_Last updated for: feature 005-launcher-shell, first pass. See PR #18 and
`specs/005-launcher-shell/` for the full spec, plan, and on-hardware checklist._
