# Validation

Version 0.2.0, checked on Windows 11 x64 on 22 November 2026.

- Built all 117 upstream protocol translation units with MSVC.
- Passed 18 native tests, including frame decoders, the simulated Keychron
  transport and JSON serialization. Release assertions are enabled.
- Passed 27 C# tests for readings, physical identities, connection state,
  caching, history, selection and native protocol metadata.
- Passed 13 comparisons against the local Stock Watcher controls and layout.
- Built the Release app and MSIX with no warnings or errors using the public
  WidBar.SDK 2.3.5 package from nuget.org.
- Registered package 0.2.0.0 with status Ok, preserving the existing package
  family and plugin ID. The packaged preview showed a responsive window.
- The source-built protocol engine completed the packaged live snapshot in
  about 1.28 seconds with no provider error. At that check, Windows reported
  the paired K2 and mouse disconnected, with no current percentages. The
  generic USB receiver remained visible without a battery level.
- Verified the package contains the built protocol engine, HeadsetControl,
  license notices and corresponding native source archives.

The original taskbar, tray, updater and Win32 UI are absent from this build.
The WinUI app uses the approved device list, detail page, settings controls,
theme resources, localization and Segoe Fluent Icons.

Hardware support across every upstream model has not been physically tested.
The engine currently runs one isolated poll per refresh. Long-running BLE
advertisement and vendor-session behavior still needs hardware verification.
ARM64 is configured but has not been executed on this x64 PC.
