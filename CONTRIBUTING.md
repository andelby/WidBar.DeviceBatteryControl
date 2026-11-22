# Contributing

Use DeviceBatteryControl.slnx for the widget and packaging projects.
Native protocols are built through tools/Build-Native.ps1.

Run tools/Build.ps1 -Test before submitting changes. Protocol tests use
recorded frames and simulated HID transport; they do not need physical devices.
Document hardware checks with the model, firmware and connection mode.

Keep protocols in native, UI code in XAML and C# code behind, and discovery
and persistence in Services. Use native WinUI controls and theme resources.
Add translations to both supported locales.
