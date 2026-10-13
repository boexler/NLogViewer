# Sentinel.LogViewer.Wpf

WPF controls and themes to visualize NLog log output in desktop applications.

- **Sentinel.LogViewer.Wpf** — core viewer control (targets `net8-windows`).
- **Sentinel.LogViewer.Wpf.MaterialDesign** — Material Design styling on top of the core library.

```xml
<PackageReference Include="Sentinel.LogViewer.Wpf" Version="…" />
```

```xml
xmlns:lv="clr-namespace:Sentinel.LogViewer.Wpf;assembly=Sentinel.LogViewer.Wpf"
<!-- … -->
<lv:LogViewer />
```

**Migration from Sentinel.NLogViewer:** replace the package id with `Sentinel.LogViewer.Wpf` (or `Sentinel.LogViewer.Wpf.MaterialDesign`) and rename the control type from `NLogViewer` to `LogViewer`. This is a breaking change; bump major when publishing.

Repository: [https://github.com/boexler/NLogViewer](https://github.com/boexler/NLogViewer)
