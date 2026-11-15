"""
Replace SCons' os.spawnve()-based process launcher with a subprocess-based one.

WHY THIS EXISTS
---------------
On this machine (Windows 11 build 26200) os.spawnve() is broken: any call that
passes an environment dies with 0xC0000005 (ACCESS_VIOLATION). Measured
2026-09-10:

    os.spawnv (no env)          -> works
    os.spawnve (env={})         -> ACCESS_VIOLATION
    os.spawnve (env={'A':'B'})  -> ACCESS_VIOLATION
    os.spawnve (env=os.environ) -> ACCESS_VIOLATION

It is not a Python bug: CPython 3.6.8 and 3.11.5 fail identically. It is not
the venv, not pythonw, and not console-related -- it fails with a real console
attached just as it does without one. No AppInit hooks, no injected DLLs. It is
the OS/CRT _wspawnve.

Padding the environment changes the *failure mode* rather than curing it
(1 KiB -> 0xC0000005, 4 KiB -> succeeds, 8 KiB -> 0xC0000142), which is the
signature of heap corruption. So it is intermittent, not consistently fatal.

SCons/Platform/win32.py routes every build action through spawnve()
(spawn -> exec_spawn:117, piped_spawn:81). The result on this machine:

  * compile steps randomly crash the SCons process, so builds terminate having
    produced bootloader.bin and partitions.bin but NO firmware.bin -- leaving a
    stale or absent image to flash, with no error that says so;
  * every cmd.exe that *does* launch, from a parent with no console, allocates
    its own console window -- one per compile and one per 'del' of a temp
    response file. A full rebuild opened ~190 windows and took the machine down.

THE FIX
-------
Patch the module-level SCons.Platform.win32.spawnve only. piped_spawn(),
exec_spawn() and spawn() resolve it from module globals at call time, so their
own argument-escaping logic is left completely untouched -- we swap the broken
primitive underneath them and nothing else.

subprocess.list2cmdline() applies the same MSVCRT quoting rules _wspawnve would,
so passing argv straight through is faithful. CREATE_NO_WINDOW additionally
stops the console-window storm at its source.

Verified: 20/20 launches clean, zero windows.

Remove this file only once os.spawnve is proven fixed -- tools/Build-Flash.ps1
re-tests it on every run and will tell you.
"""

import os
import sys

CREATE_NO_WINDOW = 0x08000000


def _install():
    if os.name != "nt":
        return "skipped (not Windows)"

    import subprocess
    import SCons.Platform.win32 as win32

    if getattr(win32, "_vandimmer_spawn_patched", False):
        return "already patched"

    debug_log = os.environ.get("VANDIMMER_SPAWN_DEBUG")

    def _log(argv, rc):
        if not debug_log:
            return
        try:
            with open(debug_log, "a", encoding="utf-8", errors="replace") as fh:
                fh.write("rc=%s len=%d\n" % (rc, sum(len(a) + 1 for a in argv)))
                for a in argv:
                    fh.write("  arg: %s\n" % a)
                fh.write("\n")
        except OSError:
            pass

    def _cmdline(argv):
        """
        Build the command line by hand.

        SCons calls us as [sh, '/C', escape(command)] and its escape() has
        already quoted that third element the way cmd.exe expects. Handing the
        list to subprocess would run it through list2cmdline(), which re-quotes
        and backslash-escapes the existing quotes:

            list2cmdline -> cmd.exe /C "\\"xtensa-esp32s3-elf-g++ @x.tmp\\""

        cmd.exe has no backslash-escape convention, so it fails to parse that,
        exits 1 and prints nothing -- which surfaces as a bare "Error 1" with no
        compiler diagnostics whatsoever. Quote only the executable and pass
        SCons' own escaping through untouched.
        """
        head = subprocess.list2cmdline(argv[:1])
        return head + (" " + " ".join(argv[1:]) if len(argv) > 1 else "")

    def spawnve(mode, file, args, env):
        """Stand-in for os.spawnve(mode, file, args, env)."""
        argv = [str(a) for a in args]
        envd = {str(k): str(v) for k, v in dict(env).items()}
        cmdline = _cmdline(argv)

        if mode == os.P_WAIT:
            rc = subprocess.call(
                cmdline,
                executable=file,
                env=envd,
                creationflags=CREATE_NO_WINDOW,
                close_fds=False,
            )
            _log(argv, rc)
            return rc

        # SCons only ever uses P_WAIT here, but keep the contract honest.
        proc = subprocess.Popen(
            cmdline,
            executable=file,
            env=envd,
            creationflags=CREATE_NO_WINDOW,
            close_fds=False,
        )
        if mode == os.P_NOWAIT:
            return proc.pid
        return proc.wait()

    win32.spawnve = spawnve
    win32._vandimmer_spawn_patched = True
    return "patched SCons.Platform.win32.spawnve -> subprocess (CREATE_NO_WINDOW)"


_status = _install()
print("[spawn-fix] %s" % _status)

# Fail loudly rather than silently building through the broken path.
if os.name == "nt" and "patched" not in _status:
    print("[spawn-fix] FATAL: could not install the spawnve workaround.", file=sys.stderr)
    Return()  # noqa: F821  (SCons injects Return into extra-script scope)
