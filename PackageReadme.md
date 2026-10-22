# Sentinel.LogViewer.Wpf

WPF controls and themes to visualize logs in desktop applications.

- **Sentinel.LogViewer.Wpf** — the viewer control (targets `net8-windows`).
- **Sentinel.LogViewer.Core** — the log entry, the sources of entries and the logic without user interface (targets `net8.0`, no WPF). The Wpf package brings it along.
- **Sentinel.LogViewer.Extensions.Logging** — `AddLogViewer()` for Microsoft.Extensions.Logging: what an application logs through `ILogger` appears in the viewer (targets `net8.0`).

```xml
<PackageReference Include="Sentinel.LogViewer.Wpf" Version="…" />
```

```xml
xmlns:lv="clr-namespace:Sentinel.LogViewer.Wpf;assembly=Sentinel.LogViewer.Wpf"
<!-- … -->
<lv:LogViewer />
```

A viewer without any setting shows `LogBuffer.Default`. Add the logging once at the start of the application, with or without dependency injection:

```csharp
builder.Logging.AddLogViewer();                         // host builder, or LoggerFactory.Create(b => b.AddLogViewer())
services.AddLogViewer(o => o.Capacity = 5000);          // with dependency injection
```

**NLog:** the control no longer references NLog, and `CacheTarget` and `TargetName` are gone. Applications that log through `ILogger` are seen as before, also when NLog runs behind `ILogger`. Applications that log with NLog directly (`LogManager.GetLogger`) send their logs to the viewer over the network (Log4JXml target).

**Migration from Sentinel.NLogViewer:** replace the package id with `Sentinel.LogViewer.Wpf` and rename the control type from `NLogViewer` to `LogViewer`. This is a breaking change; bump major when publishing. There is no successor of `Sentinel.NLogViewer.MaterialDesign`.

Repository: [https://github.com/boexler/NLogViewer](https://github.com/boexler/NLogViewer)
