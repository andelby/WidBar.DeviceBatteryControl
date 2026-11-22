# Device Battery Control

A Windows 11 battery widget for WidBar, built with WinUI 3 and C#.

This is a fork of [BatteryHub](https://github.com/krisgfx9/Wireless-Device-Battery-Monitor).
It keeps the device protocols and replaces the original taskbar and tray UI
with a WidBar SDK widget. The protocol executable is built from this repository;
it does not launch BatteryHub or require a separate BatteryHub installation.

## Project layout

- `native/src`: device discovery, HID transport and vendor protocols.
- `native/protocol_main.cpp`: a console entry point that returns device JSON.
- `src/Widget/Pages`: device list, details and settings with XAML and code behind.
- `src/Widget/Controls`: taskbar preview, device rows, charts and settings cards.
- `src/Widget/Services`: discovery, protocol process, refresh and history storage.
- `src/Widget/Models`: device data, settings and history rules.
- `src/Widget/Hosting`: WidBar SDK integration.
- `src/Widget/Shell`: standalone preview window.
- `src/Widget/Styles`, `Localization` and `Strings`: styling and translations.
- `packaging`: MSIX identity and WidBar app extension.
- `native/tests`, `tests/Widget.Tests`: protocol and widget tests.

The widget supports light/dark themes and English/Italian UI resources. Device
icons use Segoe Fluent Icons throughout. Layout, controls and hover behavior
follow the existing Weather and Stock Watcher widgets.

Settings include native icon choices for mouse, keyboard and headphones, system
or custom colors, device names or short labels, and switches for icons, the
percent symbol and low-battery highlighting. Changes appear in the live preview.
Choose up to ten devices automatically or select specific registered devices.
The widget hides from the taskbar when none of the selected devices is connected;
"Show even without devices" can keep it visible and is off by default.
Taskbar layout options include three density levels, a side-by-side or two-row
arrangement, label position and width, separate label/value text sizes, and icon
size and position. The two-row layout keeps labels beside values and limits sizes
to fit the 48-DIP taskbar preview. The live settings preview uses the same layout
metrics as the installed widget.

The scrolling footer links to this project and the original repository, identifies
the project as a fork, and thanks krisgfx9 and the contributors for their work.

The history chart uses the same line, gradient, marker and entrance treatment
as Stock Watcher. Its horizontal axis covers the real readings available within
the saved history. A first reading appears in the center; later readings span
the plot according to their actual timestamps. Missing observations remain gaps.
The vertical scale adapts to the readings automatically. Hovering
shows the recorded value and time. The display never invents battery readings.

## Build

Install Visual Studio 2026 with the C++ desktop and WinUI tools, .NET 10, the
Windows 11 SDK, PowerShell 7 and CMake. The native engine uses MSVC and a static
C++ runtime. The scripts locate Visual Studio with vswhere.

```powershell
./tools/Build.ps1 -Test -Package
```

The first build downloads the pinned HeadsetControl 4.3.0 helper and its source
archive, with checksum verification. No BatteryHub executable is downloaded.
The MSIX contains the native engine, helper, licenses and protocol sources.

For a faster development build:

```powershell
dotnet build src/Widget/DeviceBatteryControl.Widget.csproj -c Debug -p:Platform=x64 -p:RuntimeIdentifier=win-x64
./src/Widget/bin/x64/Debug/net10.0-windows10.0.19041.0/win-x64/WidBar.DeviceBatteryControl.Widget.exe --preview
```

Add `--demo` for sample devices or `--lang it-IT` to preview Italian. Use
`--settings` to open settings or `--device demo-mouse` to open a demo detail page.
Add `--compact` to preview device names in the compact two-row arrangement.
The regular WidBar widget always uses live discovery. Install the MSIX and add Device Battery
Control from WidBar's widget picker.

The executable inside an MSIX layout requires package-aware activation. Use the
unpackaged build above for direct preview launches; WidBar activates the installed
package through its app identity.

## Device support and resource use

The fork starts from upstream commit `52bf63eff9316c4baead8d0464d6fab4d159b588`.
All 117 protocol translation units are part of the native build. Driver order
and device matching are retained. Hardware support depends on model, connection
mode and firmware; see the upstream device list and the protocol notes in `docs`.

Protocol results merge with Windows Bluetooth/HID inventory by physical identity.
Connected devices remain visible without a percentage. Previous readings retain
their timestamp and expire after 30 minutes. Missing values never become zero,
and stale values do not add chart samples.

Automatic refresh runs every 15 minutes. Taskbar instances keep device discovery
active while hidden so connecting a device can bring the widget back. Hidden
board-only instances stop polling.
One monitor is shared by widget instances in the same process. Device changes
are debounced. Protocol reads run in a hidden child process with a 45-second
deadline and bounded output; a failed driver cannot block the WinUI thread.
The process exits after each poll and releases its native sessions. Long-lived
Bluetooth advertisement/session behavior is not claimed to match the original
tray app until tested on the corresponding hardware.

Each refresh saves new real observations to an atomic JSON file. History survives
restarts and keeps up to 8,640 observations per device, about 90 days at the regular
15-minute cadence. It no longer expires after 24 hours. Registered device names
are saved alongside the samples, including devices currently disconnected.

Charts share one renderer and redraw when data, appearance, size or theme changes.
Drawing uses at most 1,024 real points while keeping extrema and gaps; the history
and hover values retain every saved observation. The entrance runs through Windows
Composition and respects the system animation setting. The native color picker
is created only when opened; appearance changes do not start device discovery.

Taskbar items use compiled x:Bind and reuse their native controls between readings.
Device details load on their first visit. History snapshots and chart projections
are cached, and hover lookup uses binary search.

## License

GPL version 3. Original authorship and history are preserved. See
[LICENSE](LICENSE) and [third-party notices](THIRD_PARTY_NOTICES.txt).
