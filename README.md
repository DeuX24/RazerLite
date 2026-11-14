# RazerLite

Ultra-lightweight, standalone Windows utility to configure legacy and Chroma Razer peripherals without Razer Synapse, accounts, or background services.

## Supported Hardware

- **Razer DeathAdder Left-Hand Edition (3.5G)** (`VID 0x1532`, `PID 0x0016`)
  - DPI: `450`, `900`, `1800`, `3500`
  - Polling Rate: `125 Hz`, `500 Hz`, `1000 Hz` (default `1000 Hz`)
  - LEDs: Scroll Wheel & Logo on/off
- **Razer BlackWidow Chroma V2** (`VID 0x1532`, `PID 0x0221`, plus common BlackWidow Chroma variants)
  - Lighting Effects: `Static`, `Spectrum`, `Wave Left`, `Wave Right`, `Breathing`, `Off`
  - Color presets + custom color picker
  - Brightness slider (`0–100%`)

## End-User Installation

1. Download **`RazerLite.exe`** from the latest [Releases](../../releases) page (or from the latest GitHub Actions workflow artifact).
2. Double-click `RazerLite.exe`—no installer, DLLs, or extra runtime downloads required on Windows 10/11 (targets built-in `.NET Framework 4.8`).
3. Optionally check **"Apply saved settings on Windows startup"** to re-apply your saved DPI/polling/lighting automatically when you log in.

## Building Locally (Windows)

Requires the .NET SDK (builds cleanly with zero NuGet packages using Windows' built-in `.NET Framework 4.8` assemblies):

```powershell
dotnet build RazerLite.csproj -c Release
```

Output executable:

```text
bin\Release\net48\RazerLite.exe
```

## Technical Notes

See [DOCUMENTATION.md](DOCUMENTATION.md) for the USB HID protocol specifications, Windows `hidusb.sys` driver notes for the DeathAdder 3.5G, and how to stop Windows Update from prompting to install Razer Synapse.
