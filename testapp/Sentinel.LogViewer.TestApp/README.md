# Sentinel LogViewer test application

One WPF application for two jobs. A window asks what to do when it starts.

## Log generator

Sends test logs to the viewer application, to try its receiving side.

- **Library:** NLog 6, log4net (UDP only, it has no TCP appender) or plain XML that the test application writes itself.
- **Protocol and host:** UDP or TCP; the host is a drop-down (`127.0.0.1`, `0.0.0.0`, `255.255.255.255` for a broadcast over UDP, the addresses of this computer) and can be typed.
- **Generator:** a log every interval, a share of them with an exception, several application names (plain XML sends a random one, NLog the first, log4net the name of the process).
- **Send a message...:** write one message (level, application, logger, text, exception, properties) and send it. The log4j XML that goes out is shown and can be edited as raw text, to send broken XML on purpose.
- **Edge cases:** one event in pieces over TCP, broken XML, a very large event, special characters and emoji.

## Control test

A window with log viewer controls, also in popups and with tabs for tasks. The logging is connected in one of three ways, chosen in the start window:

| Way | File |
| --- | --- |
| Logging of .NET without dependency injection | `Embedding/NoDi.cs` |
| Generic Host with dependency injection | `Embedding/HostWithDi.cs` |
| Host and NLog behind `ILogger` (also writes a file) | `Embedding/HostWithNLog.cs` |

**A template:** the files in `Embedding/` are short and commented; copy the one that fits your application. The control needs no setting: it shows `LogBuffer.Default`, which `AddLogViewer()` writes to. A viewer can also be given a `Source` to show a part of it (`ControlTest/FilteredSource.cs`).

To try another way, use *Back to start*: the logging is built once at the start and cannot be switched while running.

## Run, install

```powershell
dotnet run --project testapp/Sentinel.LogViewer.TestApp
```

The releases have an installer (`Sentinel.LogViewer.TestApp-<version>-x64.msi`, it installs next to the viewer application) and a portable ZIP (`Sentinel.LogViewer.TestApp-<version>-portable.zip`). There is no Chocolatey package, no NuGet package and no update check.
