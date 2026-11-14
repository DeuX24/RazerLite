# RazerLite — Hardware & Design Documentation

Reference documentation for building a lightweight, Synapse-free Windows utility for the **Razer DeathAdder Left-Hand Edition (3.5G)** and **Razer BlackWidow Chroma V2**.

---

## 1. Target Hardware & USB IDs

| Device | Vendor ID (VID) | Product ID (PID) | Notes |
| :--- | :--- | :--- | :--- |
| **Razer DeathAdder Left-Hand Edition (3.5G)** | `0x1532` | `0x0016` | Shares core 3.5G architecture and USB PID with the right-handed DeathAdder 3.5G. |
| **Razer BlackWidow Chroma V2** | `0x1532` | `0x0221` | Standard Chroma 90-byte HID protocol. *(Other BlackWidow models use different PIDs, so a device scanner should verify the exact PID if it is a different revision.)* |

---

## 2. Hardware Protocols (OpenRazer Reference)

### A. DeathAdder 3.5G / Left-Hand Edition (`1532:0016`)
Unlike newer Razer peripherals, the DeathAdder 3.5G does **not** use the 90-byte Chroma report structure. In OpenRazer (`razermouse_driver.c`), configuration is held in a 3-byte `da3_5g` payload sent as a legacy USB control transfer:

```c
struct {
    unsigned char leds;
    unsigned char dpi;
    unsigned char poll;
};
```

* **Control Transfer Parameters:** Sent via `razer_send_control_msg_old_device` with `wValue = 0x10`, `wIndex = 0x00`, `size = 3` (`sizeof(da3_5g)`).
* **LED Bitmask (`leds`):**
  * `0x01`: Logo LED
  * `0x02`: Scroll Wheel LED
* **DPI Mapping (`dpi`):**
  * `450 DPI` → `4`
  * `900 DPI` → `3`
  * `1800 DPI` → `2`
  * `3500 DPI` → `1`
* **Polling Rate Mapping (`poll`):**
  * `1000 Hz` → `1`
  * `500 Hz` → `2` (default fallback)
  * `125 Hz` → `3`

---

### B. BlackWidow Chroma V2 (`1532:0221`)
Uses the standard 90-byte `struct razer_report` transmitted over USB HID (`HID_REQ_SET_REPORT` / `HidD_SetFeature` on Windows, prefixed with a 1-byte HID Report ID `0x00` for a 91-byte buffer):

| Byte Offset (in 90-byte report) | Size | Field | Description |
| :--- | :--- | :--- | :--- |
| `0` | 1 | `status` | `0x00` for outgoing requests |
| `1` | 1 | `transaction_id` | Device/protocol transaction ID (commonly `0x1F` / `0x3F`) |
| `2..3` | 2 | `remaining_pkts` | Remaining packet count (`0x0000`) |
| `4` | 1 | `protocol_type` | `0x00` |
| `5` | 1 | `data_size` | Number of active bytes in `arguments` |
| `6` | 1 | `command_class` | Command category (e.g., `0x03` for LED/lighting) |
| `7` | 1 | `command_id` | Specific command (e.g., `0x0A` for LED effects) |
| `8..87` | 80 | `arguments` | Command payload (zero-padded); e.g., Static effect uses `args[0] = 0x06`, followed by `R, G, B` |
| `88` | 1 | `crc` | XOR checksum of report bytes `2` through `87` (inclusive) |
| `89` | 1 | `reserved` | `0x00` |

---

## 3. Windows Driver & OS Considerations

### Preventing Automatic Razer Software Prompts
Razer mice do not embed an installer inside the mouse firmware itself. Instead, **Windows Update** detects `VID_1532` on plug-in and downloads Razer's signed WHQL driver package, whose bundled co-installer automatically launches the Synapse setup prompt.

To prevent this on Windows 10/11:
1. Open **Device Manager** → **Mice and other pointing devices**, uninstall the Razer mouse device with **"Attempt to remove the driver for this device"** checked, and let it fall back to Microsoft's built-in **HID-compliant mouse** driver.
2. In Windows **Change device installation settings**, select **No** (*"Do you want to automatically download manufacturers' apps and custom icons available for your devices?"*).

### Windows `hidusb.sys` Limitation on the DeathAdder 3.5G
* **BlackWidow Chroma V2 (`0x0221`):** Uses standard HID Feature Reports, which Windows' `hidusb.sys` passes through cleanly via `HidD_SetFeature` without extra drivers.
* **DeathAdder 3.5G (`0x0016`):** Because it uses a legacy USB control transfer (`wValue = 0x10`) rather than a standard HID Feature Report descriptor entry, Windows 10/11's built-in `hidusb.sys` driver may reject `HidD_SetFeature` from user-space applications.
  * **Do not** replace the mouse driver with `WinUSB` (e.g., via Zadig), because removing `hidusb.sys` stops the mouse from functioning as a normal Windows pointer (`mouclass.sys`).
  * If standard `HidD_SetFeature` is rejected on a target PC, either a **filter driver** (such as the `libusb-win32` device filter attached on top of the HID driver) or Razer's legacy **v3.05 standalone driver** (in Windows 7 compatibility mode) is required to pass that control packet.

---

## 4. Existing Alternatives vs. Custom Lightweight GUI Architecture

### Existing Windows Options
* **Razer Legacy Standalone Driver (v3.05):** Pre-Synapse offline driver/configurator for the 3.5G / Left-Hand Edition. Built for Windows 7; may require Windows 7 compatibility mode on Windows 10/11 and can be brittle across Windows updates.
* **X-Mouse Button Control (XMBC) / AutoHotkey:** Mature, lightweight Windows utilities for remapping buttons or macros, though they cannot change hardware DPI, polling rate, or LEDs.
* **RawAccel:** Free Windows driver/GUI for custom cursor sensitivity curves without altering hardware DPI.

### Recommended Stack: C# (`.NET Framework 4.8`) Single-File `.exe` + GitHub Actions CI/CD
For the best combination of **end-user stability**, **single `.exe` distribution (zero downloads/installers)**, and a **clean custom GUI**:

* **Why C# `.NET Framework 4.8` wins over Rust for this app:**
  * **Single ~40 KB `.exe` with zero end-user downloads:** `.NET Framework 4.8` ships pre-installed in Windows 10 and Windows 11, so the user downloads one standalone `.exe` and double-clicks it—no `.NET 8` runtime, no `vcruntime` DLLs, and no extra files.
  * **Native Windows rendering (rock-solid stability):** Uses Windows' built-in WinForms/GDI+ rendering directly—avoiding GPU/OpenGL driver quirks common in Rust GPU UI toolkits (`egui`), while still supporting custom dark themes, flat vector-drawn buttons/cards, custom sliders, and Razer-green accents far more cleanly than raw Win32 controls in Rust.
  * **Zero background bloat:** Direct P/Invoke calls to `setupapi.dll` and `hid.dll` with zero external libraries; exits completely (0 MB RAM) once settings are applied.
  * **Easy local & CI builds:** Compiles locally with the installed `.NET SDK` on this machine and automatically in **GitHub Actions** on `windows-latest`.

### Custom Zero-Bloat Windows GUI Architecture
* **GUI Controls:**
  * **Mouse section:** DPI selector (`450` / `900` / `1800` / `3500`), Polling Rate selector defaulting to `1000 Hz` (`1000` / `500` / `125 Hz`), and optional Scroll Wheel / Logo LED checkboxes.
  * **Keyboard section:** Basic lighting mode (`Static`, `Spectrum`, `Wave`, `Breathing`, `Off`) and brightness slider.
  * **Device status banner:** Lists detected `VID_1532` devices and immediately reports if Windows' HID driver blocks the 3.5G legacy control packet.
* **Optional Startup Apply:** Saves selections to `%AppData%\RazerLite\config.ini` and toggles a `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` entry (`RazerLite`) so saved settings (such as `1000 Hz` + preferred DPI) apply automatically on login and exit immediately—with no terminal or manual CLI flags ever exposed to the user.

### GitHub Actions CI/CD Pipeline Design
* **Runner & Triggers:** Runs on `windows-latest` on `push`, manual `workflow_dispatch`, and version tags (`v*`).
* **Build Step:** Compiles `RazerLite.csproj` in `Release` configuration targeting `net48` (producing a single standalone `RazerLite.exe`).
* **Distribution:** Uploads `RazerLite.exe` as a downloadable **GitHub Actions artifact** on every build and attaches it automatically to a **GitHub Release** on version tags.
