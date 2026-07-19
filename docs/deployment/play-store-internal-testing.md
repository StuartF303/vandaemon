# VanDaemon UI — Play Store Internal Testing (v1)

First-version publishing guide for the **tablet** flavor of the VanDaemon app (feature `009-vandaemon-ui`).
Target track: **Internal testing** (private, up to 100 testers, available in minutes, no full review).

> **Risk class B/C (loop-playbook §4).** The build is produced and verified by the build box; the
> Play Console steps (app entry, signing enrolment, upload, testers) are **human-gated** and executed
> by Stuart. The loop does not upload.

## App identity (permanent)

| | |
|---|---|
| Display name | **VanDaemon UI** |
| `applicationId` | **`dev.vandaemon.ui`** (cannot change after first publish) |
| versionCode / versionName | `1` / `0.1.0` |
| Min / target SDK | 29 / 34 |

## Build the AAB (on `tiny`)

Toolchain (home-dir, no sudo): JDK 17 at `~/toolchain/jdk-17.0.19+10`, Android SDK at `~/Android/Sdk`.
Env is set by `~/vd-gradle.sh`.

```bash
# from the repo on tiny, on branch 009-vandaemon-ui
bash ~/vd-gradle.sh bundleTabletRelease
# -> app/build/outputs/bundle/tabletRelease/vandaemon-shell-tablet-release.aab
```

Verify:
```bash
JH=~/toolchain/jdk-17.0.19+10
$JH/bin/jarsigner -verify app/build/outputs/bundle/tabletRelease/vandaemon-shell-tablet-release.aab
# "jar verified." (a self-signed-upload-key chain warning is expected)
```

## Signing (upload key)

- Upload keystore: `app/vandaemon-upload.jks` (on `tiny`, **gitignored**).
- Password + alias: `app/keystore.properties` (on `tiny`, **gitignored** — `keyAlias=vandaemon-upload`).
- **Back both up privately** (password manager / offline). If lost, an upload key can be reset from the
  Play Console (unlike the app signing key).
- Upload cert SHA-256: `67:DD:40:D7:FC:5A:B1:BB:42:B3:6C:9C:AF:88:8E:F2:FE:B0:BF:52:97:1F:FE:D6:02:AE:94:67:1E:1F:EA:DE`
- Recommended: enable **Play App Signing** on first upload (Google holds the app signing key; this JKS
  stays the upload key only).

## Play Console steps (human)

1. **Create app** — Play Console → *Create app*. Name **VanDaemon UI**, App (not game), Free.
2. **Internal testing** — *Testing → Internal testing → Create new release*. Accept **Play App Signing**.
3. **Upload** `vandaemon-shell-tablet-release.aab`. (Pull it off `tiny` first, e.g.
   `scp tiny:~/projects/vandaemon/app/build/outputs/bundle/tabletRelease/vandaemon-shell-tablet-release.aab .`)
4. **Testers** — add a tester list (your Google account email). Save + roll out to Internal testing.
5. **Opt-in link** — share/open the internal-testing opt-in URL on the tablet's Google account, install
   from Play, and run the on-device checks below.
6. First-run minimal fields Google still asks for even on internal testing: app category, contact email,
   and a privacy-policy URL may be required to activate testing — supply as prompted.

## On-device verification (SC-002..004 — human)

On a tablet with the Pi reachable at `vandaemon.local:8080` (or a configured IP):

- [ ] **SC-002** — set VanDaemon UI as the home app; Home/boot lands on the live dashboard; live values update.
- [ ] **SC-003** — start with the Pi OFF → the connection/waiting screen shows (never blank); power the Pi
      on → it auto-transitions to the UI.
- [ ] **SC-004** — change the address on the connection screen, restart the app → it still targets the new address.
- [ ] Home is a **soft default**: other apps remain usable; un-setting home fully restores prior behaviour.

If `vandaemon.local` doesn't resolve on the tablet, enter the Pi's IP on the connection screen (persisted).
mDNS service discovery (NsdManager) is a deferred fast-follow if OS resolution proves unreliable.
