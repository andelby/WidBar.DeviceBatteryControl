# Validation

## Version 0.4.0

Checked on Windows 11 x64 on 22 November 2026.

- Passed 44 C# tests and 13 comparisons against Stock Watcher controls/layout.
- A cold disk reload retained samples from 45 days ago and the saved device name.
  JSON writes replace the history file atomically after serialization completes.
- History keeps at most 8,640 real samples per device without a 24-hour cutoff.
  Chart drawing stays within 1,024 real points, preserving extrema and gaps;
  hover lookup still uses the full set of saved samples.
- Automatic refresh is fixed at 15 minutes. Legacy refresh/scale preferences
  no longer appear in the settings schema or the UI.
- Tested automatic and explicit selections up to ten devices, selection
  normalization, empty selections, receiver-only and disconnected cases,
  default hiding and the opt-in empty-state display.
- Taskbar visibility uses the SDK's IsPreviewVisible and RequestPreviewRefresh
  signals, as Media does. A five-second grace period handles brief connection
  loss and an open flyout holds the widget visible. Discovery remains active
  while an empty taskbar instance is hidden, allowing it to reappear.
- Removed the history description and chart-scale/refresh controls. The footer
  is inside the settings scroll content and contains the widget name and GitHub.
- Both locales have the same 123 resource keys; all referenced keys resolve.
- Release compilation passed with no warnings or errors.

The Windows UI controller still fails before initialization, so interaction and
visual checks through automation remain unavailable.

## Previous baseline

Version 0.3.0, checked on Windows 11 x64 on 22 November 2026.

- Built all 117 upstream protocol translation units with MSVC.
- Passed 18 native tests, including frame decoders, the simulated Keychron
  transport and JSON serialization. Release assertions are enabled.
- Passed 39 C# tests for readings, physical identities, connection state,
  caching, history, selection, chart time/range projection, appearance settings
  and native protocol metadata. Chart cases cover a centered first reading,
  irregular intervals, duplicate/out-of-order timestamps, missing observations,
  flat values and slow polling with scheduling jitter.
- Passed 13 comparisons against the local Stock Watcher controls and layout.
- Built the Release app and MSIX with no warnings or errors using the public
  WidBar.SDK 2.3.5 package from nuget.org.
- Registered package 0.3.0.0 with status Ok, preserving the existing package
  family and plugin ID. The installed taskbar worker reconnected to WidBar and
  reported a ready, visible direct preview in its SDK log.
- Opened the demo chart and settings through the package activation manager,
  with responsive top-level windows and no app error log. The installed app DLL
  matches the published Release DLL. Launching the layout executable directly
  without package identity failed; package-aware activation resolved this.
- Both resource files contain the same 129 keys, including the new controls,
  icon choices, color choices and fork attribution in English and Italian.
- The source-built protocol engine previously completed the packaged live snapshot in
  about 1.28 seconds with no provider error. At that check, Windows reported
  the paired K2 and mouse disconnected, with no current percentages. The
  generic USB receiver remained visible without a battery level.
- Verified the package contains the built protocol engine, HeadsetControl,
  license notices and corresponding native source archives.

The original taskbar, tray, updater and Win32 UI are absent from this build.
The WinUI app uses the approved device list, detail page, settings controls,
theme resources, localization and Segoe Fluent Icons.

The chart follows Stock Watcher's 2 px rounded line, gradient fill, final marker,
58 px price-axis layout and 700 ms composition reveal. Its x positions use the
actual elapsed time from the first to last retained reading, rather than placing
a short history at the end of a fixed 24-hour canvas. Percentages and timestamps
remain real observations; no synthetic smoothing is stored.

Settings update the SDK draft immediately, so a last edit is included when Save
is pressed and no delayed update runs after Cancel. The existing settings JSON
still selects the original icons and compact taskbar layout by default.

A short 10.02-second local sample used 0.1094 CPU seconds for the taskbar worker
(51.56 MB working set, 36.42 MB private memory) and 0 CPU seconds for the idle
demo chart (134.55 MB working set, 42.63 MB private memory). These are individual
samples, not a benchmark or a guarantee; GPU activity was not profiled.

The Windows UI automation controller failed to initialize in this session.
Startup and responsive-window checks passed, but pointer interactions, the
custom color picker and visual light/dark theme checks need manual verification.

Hardware support across every upstream model has not been physically tested.
The engine currently runs one isolated poll per refresh. Long-running BLE
advertisement and vendor-session behavior still needs hardware verification.
ARM64 is configured but has not been executed on this x64 PC.
