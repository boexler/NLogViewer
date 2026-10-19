# Sentinel.LogViewer.Wpf

WPF controls and themes to visualize NLog log output in desktop applications.

- **Sentinel.LogViewer.Wpf** — the viewer control (targets `net8-windows`).
- **Sentinel.LogViewer.Core** — the log entry, the sources of entries and the logic without user interface (targets `net8.0`, no WPF). The Wpf package brings it along.

```xml
<PackageReference Include="Sentinel.LogViewer.Wpf" Version="…" />
```

```xml
xmlns:lv="clr-namespace:Sentinel.LogViewer.Wpf;assembly=Sentinel.LogViewer.Wpf"
<!-- … -->
<lv:LogViewer />
```

**Migration from Sentinel.NLogViewer:** replace the package id with `Sentinel.LogViewer.Wpf` and rename the control type from `NLogViewer` to `LogViewer`. This is a breaking change; bump major when publishing. There is no successor of `Sentinel.NLogViewer.MaterialDesign`.

Repository: [https://github.com/boexler/NLogViewer](https://github.com/boexler/NLogViewer)
