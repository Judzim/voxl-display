# Contributing

Thanks for taking a look. This is a small tool, so the rules are short.

## Building

```cmd
build.cmd
```

It calls `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` with the manifest and
icon. Requirements: Windows 10 or 11 and nothing else. Do not add a NuGet dependency
without a good reason: the app is meant to build on a machine with no SDK installed.

Sources:

| File | What is in it |
|---|---|
| `VoxlDisplay.cs` | the window, the settings file, the Win32 display calls, starting voxl |
| `Qr.cs` | the QR encoder (byte mode, error correction L, versions 1 to 4) |
| `app.manifest` | DPI awareness and the as-invoker execution level |

`Qr.cs` is verified against a reference implementation: for the same input, the module
matrix must be identical. If you change it, keep that property.

## Installer

`install.ps1` and `install-vdd.ps1` must:

- be idempotent, so running them twice never creates a second virtual monitor,
- fail loudly, never skip a missing piece silently,
- work offline when `vendor/` contains the packages,
- leave the machine usable when they fail halfway.

Test a change to the installer on a machine where the driver is **not** installed. A
machine that already has it will not exercise most of the code.

## Pull requests

- English in code, comments, commit messages and documentation.
- One topic per commit.
- No secrets, no `settings.json`, no screenshots of someone's desktop, no built binaries
  in a commit. Releases carry the binaries, git carries the source.
- If you change the UI, include a screenshot of the result in the pull request.
- If you touch the display or DPI calls, say which Windows build you tested on.

## Reporting a problem

Please include:

- Windows version and build (`winver`),
- the app version (the release tag) and `voxl.exe --version`,
- the contents of `voxl.log` next to the app,
- the GPU name, and whether the PC is a laptop with switchable graphics.

The last one matters: DXGI desktop duplication behaves differently on hybrid laptops.
