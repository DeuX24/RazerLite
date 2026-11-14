# AGENTS.md — Handoff & Engineering Guide for RazerLite

This document provides everything an AI agent or developer needs to maintain, test, and extend **RazerLite** without re-discovering project constraints, build quirks, or USB protocol details.

---

## 1. Core Design Constraints (Do Not Violate)

1. **Single Standalone `.exe` Distribution:**
   - The end user must only ever download and run a single **`RazerLite.exe`** file—no installer, no extra `.dll` files, and no `.NET` runtime downloads.
   - Targets **`.NET Framework 4.8` (`net48`)**, which is pre-installed on Windows 10 and Windows 11.
   - **Zero NuGet packages** and **zero third-party dependencies**. All USB HID, registry, and UI features use standard `.NET Framework 4.8` APIs and direct Win32 P/Invoke.
   - Do **not** upgrade `TargetFramework` to `net8.0-windows` unless the user explicitly agrees to self-contained ~60+ MB builds or runtime prerequisites.
2. **Offline / Local Build Configuration (`RazerLite.csproj`):**
   - `RazerLite.csproj` uses `<AutomaticallyUseReferenceAssemblyPackages>false</AutomaticallyUseReferenceAssemblyPackages>` and `<FrameworkPathOverride Condition="Exists('C:\Windows\Microsoft.NET\Framework64\v4.0.30319')">C:\Windows\Microsoft.NET\Framework64\v4.0.30319</FrameworkPathOverride>` with explicit `<Reference>` entries (`System`, `System.Core`, `System.Drawing`, `System.Windows.Forms`).
   - Do not remove these settings; they allow `dotnet build -c Release` to succeed offline in ~2 seconds both locally and on GitHub Actions `windows-latest`.
   - Maintain `net48` compatibility (avoid C# features requiring `IsExternalInit` like positional `record` types or `init` properties).
3. **100% GUI for the End User / Zero Background Daemon:**
   - The user does **not** want any command-line workflow, key remapping, game mode, or per-game profiles.
   - The app applies settings directly to hardware and exits—using **0 MB RAM** when closed.
   - `--startup-apply` is strictly internal for the `HKCU\...\Run` checkbox so saved settings re-apply silently on Windows login.

---

## 2. Repository Architecture & File Map

| File | Responsibility |
| :--- | :--- |
| `RazerLite.csproj` | SDK-style `net48` WinExe project configured for zero-NuGet offline builds against Windows' built-in .NET Framework 4.8 assemblies. |
| `Program.cs` | Entry point. Calls `SetProcessDPIAware()`, handles internal `--startup-apply` silent execution, or launches `MainForm`. |
| `MainForm.cs` | Dark-themed WinForms UI (`#121316` window, `#1C1E24` cards, `#00E65B` Razer-green accents). Includes device detection banner, DeathAdder DPI (`450`/`900`/`1800`/`3500`), polling rate (`125`/`500`/`1000 Hz`, default `1000 Hz`), LED checkboxes, BlackWidow effect/color/brightness controls, startup toggle, and status bar. |
| `AppConfig.cs` | Reads/writes `%AppData%\RazerLite\config.ini` (plain key=value format so no JSON dependency is needed) and manages `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\RazerLite`. |
| `HidNative.cs` | Win32 P/Invoke (`setupapi.dll`, `hid.dll`, `kernel32.dll`) to enumerate `VID_1532` HID device interfaces (`EnumerateRazerDevices`) and send/read HID feature reports (`HidD_SetFeature` / `HidD_GetFeature`). Tries `dwDesiredAccess = 0` first (so OS-claimed mice/keyboards open cleanly), then falls back to `GENERIC_READ \| GENERIC_WRITE`. |
| `RazerProtocol.cs` | Encodes hardware commands for DeathAdder 3.5G (`0x0016`) and BlackWidow Chroma V2 (`0x0221` + fallback Chroma PIDs). |
| `.github/workflows/build.yml` | GitHub Actions workflow on `windows-latest`: builds `Release`, verifies `bin\Release\net48\RazerLite.exe`, uploads `RazerLite-win-exe` artifact on push/PR/dispatch, and creates a GitHub Release on `v*` tags. |
| `DOCUMENTATION.md` | Protocol specifications, Windows Update Synapse-installer suppression steps, and `hidusb.sys` driver notes. |
| `README.md` | End-user overview, download instructions, and local build guide. |

---

## 3. Build, Test & Release Commands

Run from `C:\Users\danni\Documents\GitHub\RazerLite`:

* **Local Release Build (~2s):**
  ```powershell
  dotnet build RazerLite.csproj -c Release
  ```
  Output: `bin\Release\net48\RazerLite.exe` (~33 KB).

* **Standalone Smoke Test (verifies `.exe` runs with no `.config` or sidecar files):**
  ```powershell
  Start-Process -FilePath bin\Release\net48\RazerLite.exe -ArgumentList '--startup-apply' -Wait -PassThru
  ```
  Expected exit code: `0`.

* **Publish a User-Downloadable GitHub Release:**
  ```powershell
  git tag v1.0.0
  git push origin v1.0.0
  ```
  GitHub Actions automatically builds `RazerLite.exe` and attaches it to the `v1.0.0` GitHub Release.

---

## 4. On-Device Hardware Testing & Next Steps Checklist

> **Important:** As of handoff, the physical mouse and keyboard were **not** connected to the development PC, so USB HID packet acceptance has not yet been verified against live hardware.

### A. Testing the Razer DeathAdder Left-Hand Edition (3.5G, `1532:0016`)
1. Plug in the mouse and click **Refresh Devices** in `RazerLite.exe`. Confirm `[0x0016]` appears in the device banner.
2. Select desired **DPI** and **1000 Hz**, then click **Apply Mouse**.
3. **If `HidD_SetFeature` succeeds:** Verify cursor sensitivity changes across `450` / `900` / `1800` / `3500` and polling rate reads `1000 Hz` in a polling-rate checker.
4. **If Windows reports `Win32 error ...` (`hidusb.sys` blocked the legacy `wValue = 0x10` control transfer):**
   - **Never** replace `hidusb.sys` directly with `WinUSB` (that stops the mouse from working as a Windows pointer).
   - Instead, inspect whether Razer's legacy v3.05 driver or a `libusb-win32` device filter allows the control transfer, and—if adopting `libusb-win32` filter—keep it optional or statically/cleanly handled in `RazerProtocol.cs` without breaking zero-dependency UX for users who don't need it.

### B. Testing the Razer BlackWidow (`1532:0221` or Variant)
1. Plug in the keyboard and click **Refresh Devices**.
2. Check the exact PID shown in the device banner (`[0xXXXX]`):
   - `0x0221` is BlackWidow Chroma V2.
   - `KnownBlackWidowPids` in `RazerProtocol.cs` also covers `0x0203`, `0x0209`, `0x0216`, `0x021A`, and `0x0214`.
   - If the user's keyboard shows a different PID (or if an older BlackWidow uses a non-Chroma single-color protocol or `transaction_id = 0x3F` instead of `0x1F`), update `KnownBlackWidowPids` / `ApplyBlackWidow` in `RazerProtocol.cs` accordingly (and consider trying `0x3F` if `0x1F` returns no visual change on a Chroma V2).
3. Test `Static`, `Spectrum`, `Wave L`, `Wave R`, `Breathing`, `Off`, and the **Brightness** slider on hardware.
