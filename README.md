[1]: https://github.com/yarseyah/sentinel
[2]: https://github.com/dojo90/NLogViewer/blob/master/src/NLogViewer/Targets/CacheTarget.cs
[3]: https://github.com/yarseyah/sentinel#nlogs-nlogviewer-target-configuration

[p1]: doc/images/control.png "LogViewer"
[p2]: doc/images/overview.gif "LogViewer"
[p3]: doc/images/colors.png "LogViewer"
[p4]: doc/images/openpopup.gif "LogViewer"
[p5]: doc/images/newtask.gif "LogViewer"
[p6]: doc/images/logviewercolumns.png "Column Visibility Properties"
[p7]: doc/images/filters1.png "Filter Buttons - All Visible"
[p8]: doc/images/filters2.png "Filter Buttons - Some Hidden"
[p9]: doc/images/controls1.png "Control Buttons - All Visible"
[p10]: doc/images/controls2.png "Control Buttons - Hidden"

[nuget]: https://nuget.org/packages/Sentinel.LogViewer.Wpf/

## Nuget

[![NuGet](https://img.shields.io/nuget/v/sentinel.logviewer.wpf.svg "nuget")](https://www.nuget.org/packages/Sentinel.LogViewer.Wpf)
[![NuGetDownloads](https://img.shields.io/nuget/dt/sentinel.logviewer.wpf.svg "nuget downloads")](https://www.nuget.org/packages/Sentinel.LogViewer.Wpf)

A NuGet-package is available [here][nuget].

Sentinel.LogViewer
=================

LogViewer is a ui control library to visualize NLog logs in your personal application. It is mainly based on [Sentinel][1] and its controls.

supported Framework: `.NET8`

## Desktop application (Sentinel.LogViewer)

GitHub **Releases** ship:

- **MSI installer** (`Sentinel.LogViewer-<version>-win-x64.msi`): installs under Program Files with a license dialog (MIT), Start Menu shortcut, and optional features (desktop shortcut, `.log` association) selectable in **Custom** setup. Silent install: `msiexec /i Sentinel.LogViewer-<version>-win-x64.msi /qn` (optional features with higher install level stay off unless you raise `INSTALLLEVEL` or use transforms).
- **Portable ZIP** (self-contained and framework-dependent): unpack and run `Sentinel.LogViewer.exe` from any folder; a `NLogViewer.portable` marker file directs settings to `appsettings.json` beside the app (see `README-Portable.txt` in the ZIP). Installed (MSI) copies keep using per-user `%LocalAppData%\Sentinel.LogViewer\`.

Build the installer locally (Windows): `dotnet build installer/Sentinel.LogViewer.Installer.wixproj -c Release` — output `installer/bin/Release/Sentinel.LogViewer.msi`.

**Versioning:** MSI, portable ZIPs, and NuGet packages always use GitVersion `fullSemVer` (for example `3.2.0-124-wix-installer-implementation-and-portable-versions.88` on a feature branch, or `3.2.0` on a release tag). The Chocolatey Community package is the only exception; it uses a flattened prerelease such as `3.2.0-dev0000000004001`. See [chocolatey/README.md](chocolatey/README.md).

**Text log files:** the application detects the separator and the columns of a text file (timestamp, level, logger, message), and **File → Open File with Column Mapping** lets you correct them. Timestamps are read in the common formats, also when date and time are separate columns or in English: `2025-12-18 15:11:48.385`, `18.12.2025 15:11:48`, `12/18/2025 3:11:48 PM`, `18 Dec 2025 15:11:48`, `Thu Dec 18 15:11:48 2025` and syslog's `Dec 18 15:11:48` (the year is taken from the file date). Whether a date like `01/02/2025` is day first or month first is read from the dates of the file (a day above 12 decides); when the file does not tell, the culture of the computer is used, and the *Date order* setting of the mapping dialog overrides it. The format of the timestamp column in the list is a setting and can be switched per tab with a right-click on the column.

**Restarted applications:** an application is told apart by its name and its sender (IP address). When it starts again, its new process opens a new tab by default; the setting *After a restart of an application* (Settings → Receiving) can also continue in the existing tab, or open a new tab and close the old one. A new tab takes over the filters of the last tab of the application (levels, filter terms, hidden loggers and the format of the timestamp column, not the search box) and says so, with a button to remove them. Both can be set for one application in the menu of its tab, the arrow ▾ in the tab header or a right-click.

**Changelog:** after an update the application opens the changelog once, with everything that changed since the version you used before; **Help → About → What’s new** opens it at any time. It lists the release notes of all versions, can be searched, and is kept on disk so it is also available without a connection. Showing it after an update can be switched off under **Settings → Updates**.

![LogViewer][p2]

## Quick Start

### 1. Add Required References

Add the following NuGet package reference to your project:

```xml
<PackageReference Include="Sentinel.LogViewer.Wpf" Version="[version]" />
```

### 2. Configure Application Resources

In your `App.xaml`, add the LogViewer theme and styles:

```xml
<Application x:Class="YourApp.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:xamlConverter="clr-namespace:Sentinel.LogViewer.Wpf.XamlConverter;assembly=Sentinel.LogViewer.Wpf"
             xmlns:xamlMultiValueConverter="clr-namespace:Sentinel.LogViewer.Wpf.XamlMultiValueConverter;assembly=Sentinel.LogViewer.Wpf"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <!-- LogViewer Theme -->
                <ResourceDictionary Source="pack://application:,,,/Sentinel.LogViewer.Wpf;component/Themes/Generic.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

### 3. Use the Control

Add the namespace and use the control in your XAML:

```xaml
<Window x:Class="YourApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:dj="clr-namespace:Sentinel.LogViewer.Wpf;assembly=Sentinel.LogViewer.Wpf">
    <Grid>
        <dj:LogViewer />
    </Grid>
</Window>
```

### NLog Configuration

`LogViewer` is subscribing to [CacheTarget][2]. By default, the `LogViewer` is automatically creating a [CacheTarget][2] with `loggingPattern  "*"` and `LogLevel "Trace"`.

If you want to customize the `loggingPattern` and `LogLevel`, add the following to your `Nlog.config`.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog 
  xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:schemaLocation="http://www.nlog-project.org/schemas/NLog.xsd NLog.xsd"
  autoReload="true">

  <extensions> 
    <add assembly="Sentinel.LogViewer.Wpf" /> 
  </extensions> 

  <targets async="true">
    <target
      xsi:type="CacheTarget"
      name="cache"/>
  </targets>

  <rules>
    <logger name="*" writeTo="cache" minlevel="Debug"/> 
  </rules>
</nlog>
```

## Customize

### Control Buttons

The LogViewer includes control buttons that allow you to manage log viewing behavior. The control buttons are organized in a GroupBox and can be controlled programmatically.

![Control Buttons - All Visible][p9]

![Control Buttons - Hidden][p10]

**Control Properties:**
- `AutoScroll` - Automatically scroll to the newest log entry when new entries are added
- `ClearCommand` - Command to clear all log entries from the viewer
- `Pause` - Pause or resume logging to improve performance when not actively monitoring
- `ShowControlButtons` - Controls the visibility of the entire control button group

**Usage:**

```csharp
// Control auto-scroll behavior
logViewer.AutoScroll = true;  // Enable auto-scroll
logViewer.AutoScroll = false; // Disable auto-scroll

// Pause/resume logging
logViewer.Pause = true;  // Pause logging
logViewer.Pause = false; // Resume logging

// Hide the entire control button group
logViewer.ShowControlButtons = false;
```

**XAML Binding:**

```xaml
<dj:LogViewer 
    AutoScroll="{Binding IsAutoScrollEnabled}" 
    Pause="{Binding IsLoggingPaused}"
    ShowControlButtons="{Binding ShowControls}" />
```

**Control Logic:**
- `AutoScroll` automatically scrolls to the bottom when new log entries are added
- `ClearCommand` removes all log entries from the viewer
- `Pause` stops listening for new log events to improve performance
- The entire control group can be hidden using `ShowControlButtons = false`

### Colors

Customize `foreground` or `background` of every `logLevel`

![LogViewer][p3]

### Multi targeting

Use more than one instance of `LogViewer` to match different `rules`.

Create 2 `targets` with their own `rules`.

```xml
<targets async="true">
  <target xsi:type="CacheTarget" name="target1"/>
  <target xsi:type="CacheTarget" name="target2"/>
</targets>

<rules>
  <logger name="*" writeTo="target1" maxlevel="Info"/> 
  <logger name="*" writeTo="target2" minlevel="Warn"/> 
</rules>
```

Set `TargetName` property to link them.

```xml
<dj:LogViewer TargetName="target1"/>
<dj:LogViewer TargetName="target2"/>
```

### Column Visibility

Control which columns are visible in the LogViewer. You can dynamically show or hide individual columns using the provided Dependency Properties.

![Column Visibility Properties][p6]

Available column visibility properties:
- `ShowIdColumn` - Controls ID column visibility
- `ShowLevelColumn` - Controls Level column visibility  
- `ShowTimeStampColumn` - Controls TimeStamp column visibility
- `ShowLoggerNameColumn` - Controls LoggerName column visibility
- Message column is always visible (no control available)

**Usage:**

```csharp
// Hide specific columns
logViewer.ShowIdColumn = false;
logViewer.ShowLevelColumn = false;

// Show columns again
logViewer.ShowIdColumn = true;
logViewer.ShowLevelColumn = true;
```

**XAML Binding:**

```xaml
<dj:LogViewer ShowIdColumn="{Binding IsIdColumnVisible}" ShowLevelColumn="{Binding IsLevelColumnVisible}" />
```

### Filter Buttons

The LogViewer includes filter buttons that allow you to hide/show specific log levels. The filter buttons are organized in a GroupBox and can be controlled programmatically.

![Filter Buttons - All Visible][p7]

![Filter Buttons - Some Hidden][p8]

**Filter Properties:**
- `TraceFilter` - Hide/show Trace level log entries
- `DebugFilter` - Hide/show Debug level log entries  
- `InfoFilter` - Hide/show Info level log entries
- `WarnFilter` - Hide/show Warn level log entries
- `ErrorFilter` - Hide/show Error level log entries
- `FatalFilter` - Hide/show Fatal level log entries
- `ShowFilterButtons` - Controls the visibility of the entire filter button group

**Usage:**

```csharp
// Hide specific log levels
logViewer.TraceFilter = true;  // Hide Trace entries
logViewer.DebugFilter = true;  // Hide Debug entries
logViewer.InfoFilter = true;   // Hide Info entries

// Show log levels again
logViewer.TraceFilter = false; // Show Trace entries
logViewer.DebugFilter = false; // Show Debug entries

// Hide the entire filter button group
logViewer.ShowFilterButtons = false;
```

**XAML Binding:**

```xaml
<dj:LogViewer 
    TraceFilter="{Binding HideTraceLogs}" 
    DebugFilter="{Binding HideDebugLogs}"
    ShowFilterButtons="{Binding ShowFilters}" />
```

**Filter Logic:**
- When a filter property is set to `true`, entries of that log level are **hidden**
- When a filter property is set to `false`, entries of that log level are **shown**
- The filter buttons are ToggleButtons that automatically bind to these properties
- The entire filter group can be hidden using `ShowFilterButtons = false`

### Filter and Search

The **Filter** hides the entries that do not match its terms; the **Search** finds entries without hiding any (Enter / F3 and Shift+Enter / Shift+F3 jump to the next and the previous hit, the hits are highlighted and counted). Both use the same matching, so they offer the same options:

- **Match case** and **Whole word**
- **Regex** - full .NET regular expressions; a pattern that is not valid gets a red frame and the reason is shown next to the box (the filter adds nothing, the search finds nothing)
- **Level** - the term only matches entries of that level
- **Patterns** - a menu of ready-made regular expressions (date, GUID, IPv4 address, URL, e-mail address, HTTP error status, exception type, one of several words) that are put into the box
- **Fields** - where to look: message, logger name, exception, thread and the other properties of the entry (default: message and logger name). The choice counts for the filter and the search together.

The filter also has **Show / Hide**: a term that hides removes the entries that match it. Every term is shown as a chip (an `−` in front of the ones that hide, `Aa` for match case, `W` for whole word, the level) that can be edited or removed. The filter shows how many of the entries it would match while you type.

**All** terms of the filter have to allow an entry (AND); a term that includes must match, a term that excludes must not.

In the desktop application the fields are a setting (Settings → Appearance → Search and filter) that new tabs start with. Choosing other fields in a tab saves them as the setting; tabs that are open stay as they are.

**Properties of the filter:**
- `CurrentSearchText` - The text in the filter box
- `UseRegexSearch`, `FilterMatchCase`, `FilterWholeWord`, `FilterIsExclude`, `FilterLevel` - The options of the next term
- `FilterError` - Why the text is not valid (read-only, `null` when it is), `FilterHitText` - e.g. "12 hits" (read-only)
- `ActiveSearchTerms` - The terms (`FilterTerm`), read-only
- `SearchHighlightBackground` - Brush used to highlight matched text

**Properties of the search:**
- `FindText`, `FindUseRegex`, `FindMatchCase`, `FindWholeWord`, `FindLevel` - The text and its options
- `FindError`, `FindResultText` - Why the text is not valid, and the count of the hits (read-only)
- `ShowFindControls` - Whether the search is shown

**Property of both:**
- `SearchFields` - The fields to look into (`SearchFields`, a combination of `Message`, `LoggerName`, `Exception`, `Thread` and `Properties`; default `Message | LoggerName`)

**Usage:**

```csharp
// Add a term that includes: entries whose message or logger contains "error", whole words only
logViewer.CurrentSearchText = "error";
logViewer.FilterWholeWord = true;
logViewer.AddSearchTerm();

// Add a regex that hides the entries it matches
logViewer.UseRegexSearch = true;
logViewer.FilterIsExclude = true;
logViewer.CurrentSearchText = @"\d{4}-\d{2}-\d{2}";
logViewer.AddSearchTerm();

// Add a term without the input: includes, or hides, a plain text
logViewer.AddRegexSearchTerm("timeout");
logViewer.AddRegexSearchTermExclude("heartbeat");

// Remove a term, or all of them
logViewer.RemoveSearchTerm(logViewer.ActiveSearchTerms.First());
logViewer.ClearAllSearchTerms();

// Look into the exception and the other properties as well
logViewer.SearchFields = SearchFields.All;

// Find without hiding anything
logViewer.FindText = "timeout";
logViewer.FindMatchCase = true;
```

**XAML Binding:**

```xaml
<dj:LogViewer
    CurrentSearchText="{Binding SearchText}"
    UseRegexSearch="{Binding IsRegexMode}"
    SearchFields="{Binding SearchFields, Mode=TwoWay}"
    SearchHighlightBackground="{Binding HighlightBrush}" />
```

**Matching:**
- A text matches when an entry contains it in one of the chosen fields; without **Regex** it is a plain text, not a pattern
- The `Message` field is the message of the entry; the exception is a field of its own
- **Whole word** means that no letter, digit or underscore stands right before or after the match
- With a level, the entry has to be of exactly that level
- The matching code is `FilterTerm` in `Sentinel.LogViewer.Core`, so a server or another user interface can filter in the same way

### Logger Tree

The loggers of the entries are shown as a tree on the right of the list, split at the dots of their names (`Company` → `Product` → `Service`). Open it with the **Loggers** button on the right edge of the list.

**Features:**
- A check box per logger shows or hides that logger and everything below it; a partly checked box means some of the loggers below are hidden
- Per logger: number of entries, plus counters for warnings and for errors and fatal errors; they update while entries arrive
- Right-click a logger: **Show only this logger**, **Show all loggers**, **Expand all**, **Collapse all**
- Right-click a logger name in the list: **Show only this logger**, **Hide this logger** (the same rules; `ShowOnlyLoggerCommand` and `HideLoggerCommand` take the logger name)
- A search box above the tree lists the loggers that contain the text
- Drag the splitter between the list and the tree to change its width; the arrow button in the header moves the tree to the other side of the list
- Hidden loggers work together with the level buttons and the filter terms; an entry is shown only if all of them allow it

In the desktop application the side, whether the tree is open and its width are settings (Settings → Appearance) that new tabs start with. Moving the tree, opening it or dragging the splitter in a tab saves the new value as the setting; tabs that are open stay as they are. The control itself only has the properties below, which the application hands over.

**Logger Tree Properties:**
- `ShowLoggerTree` - Whether the tree can be opened (default `true`)
- `IsLoggerTreeExpanded` - Whether the tree is open (default `false`)
- `LoggerTreeWidth` - Width of the open tree (default `260`, at least `140`); it also follows the splitter
- `IsLoggerTreeOnLeft` - Whether the tree is on the left of the list instead of the right (default `false`)
- `LoggerFilterRules` - The hidden and shown loggers as text, to save and restore them
- `LoggerTree` - The tree itself with its nodes, counters and commands

`LoggerFilterRules` holds one rule per entry: `-Company.Product` hides that logger and everything below it, `+Company.Product.Service` shows one of them again; the most specific rule wins. `-` alone stands for all loggers. Rules also work for loggers that did not log anything yet.

```csharp
logViewer.IsLoggerTreeExpanded = true;

// Hide a logger and everything below it, except one logger
logViewer.LoggerFilterRules = new[] { "-Company.Product", "+Company.Product.Service" };

// Save and restore the state
var saved = logViewer.LoggerFilterRules;
```

### Timestamp Format

The timestamp column shows the time with date and milliseconds in one of four formats of a fixed width:

| `TimeStampFormat` | Example |
|---|---|
| `System` (default) | the short date and long time of the culture of the computer, e.g. `18.12.2025 15:11:48.385` |
| `Iso` | `2025-12-18 15:11:48.385` |
| `German` | `18.12.2025 15:11:48.385` |
| `English` | `12/18/2025 03:11:48.385 PM` |

Right-click the header or a cell of the timestamp column to switch the format of that viewer; **Default view** goes back to the default.

- `DefaultTimeStampFormat` - The format of all viewers that have no choice of their own
- `CustomTimeStampFormat` - The format of this viewer, or `null` to follow the default
- `EffectiveTimeStampFormat` - The format that is shown (read-only)
- `CanChangeTimeStampFormat` - `false` while you use a `TimeStampResolver` of your own; the menu is then disabled and your resolver is left alone

### Control Architecture

LogViewer is built as a CustomControl with theming support:

**Architecture:**
- **CustomControl Base Class** - Enables proper theming and styling
- **RelayCommand System** - MVVM-compatible command system
- **Theme Support** - Full support for custom themes through resource dictionaries
- **Code Organization** - Separation of concerns between UI and logic

### Format output (ILogEntryResolver)

To format the output of a `LogEntry`, implement a new instance of `ILogEntryResolver` and bind it to the `Resolver` you want to customize:

```csharp
/// <summary>
/// Reformat the DateTime
/// </summary>
public class FooTimeStampResolver : ILogEntryResolver
{
    public string Resolve(LogEntry logEntry)
    {
        return logEntry.TimeStamp.ToUniversalTime().ToString();
    }
}
```

```csharp
LogViewer1.TimeStampResolver = new FooTimeStampResolver();
```

### Log entries (LogEntry)

The viewer shows `LogEntry` objects (package `Sentinel.LogViewer.Core`), which do not depend on a logging library:

- `Level` - `LogEntryLevel`: `Trace`, `Debug`, `Info`, `Warn`, `Error`, `Fatal` (ordered, so `entry.Level >= LogEntryLevel.Error` works)
- `LoggerName`, `Message` - the message as it is shown, with its parameters filled in
- `TimeStamp`, `Id` - the time, and a growing number that a source can replace (e.g. the line of a file)
- `Exception` - the exception as text, `null` when there is none
- `Properties` - further values by name (thread, class, the fields of a JSON line)

Events of NLog are turned into entries by `NLogEntryConverter.ToLogEntry(logEventInfo)`; the `CacheTarget` does this for you. If you push entries into the viewer yourself (`ItemsSource`), create `LogEntry` objects:

```csharp
LogViewer1.ItemsSource = new ObservableCollection<LogEntry>
{
    new LogEntry(LogEntryLevel.Info, "Acme.Shop", "Order created") { Properties = new Dictionary<string, object?> { ["Thread"] = "worker-1" } }
};
```

**Migrating from `LogEventInfo`:** `ItemsSource` takes `ObservableCollection<LogEntry>`, `ILogEventInfoResolver` is now `ILogEntryResolver` (its `Resolve` takes a `LogEntry`), `ILogEventResolverToStringConverter` is now `ILogEntryResolverToStringConverter`, `ICacheTarget` is now `ILogSource` (`Cache` is `Entries`, an `IObservable<LogEntry>`), the `CacheTarget` property of `LogViewer` is `Source`, `LogViewerBase` is part of `LogViewer`, and `FormattedMessage` is `Message`, `SequenceID` is `Id` and `Level` is a `LogEntryLevel` instead of an NLog `LogLevel`.

### The core (Sentinel.LogViewer.Core)

The package `Sentinel.LogViewer.Core` targets plain `net8.0` and has no WPF in it, so a server or another kind of user interface can use it too. The `Sentinel.LogViewer.Wpf` package brings it along. It holds:

- `LogEntry`, `LogEntryLevel` and `ILogSource`, and `LogBuffer`, a source that keeps the last entries in memory and sends them to every new subscriber
- the filters as a value (`LogViewerFilterState`), the filter term with its options and the fields it looks into (`FilterTerm`, `SearchFields`) and the format of the time stamp (`TimeStampFormat`)
- the logger tree as a model (`LoggerTree`, `LoggerNode`, `LoggerVisibilityRules`)
- the receiving of log messages over UDP (`UdpLogReceiverService`) with the Log4J XML parser (`Log4JEventParser`, `Log4JEvent`)

### Sources of the entries (Source)

There is one control, `LogViewer`. Its entries come from one of these, in this order:

1. **`ItemsSource`** - a collection of `LogEntry` that you fill yourself (the desktop application does this).
2. **`Source`** - an `ILogSource`, anything that produces `LogEntry` objects.
3. **The logging of NLog** - without either of them the viewer shows the logging of the application through a `CacheTarget` (see above), so dropping `<dj:LogViewer />` into a window is enough. `TargetName` picks one of several `CacheTarget`s.

An `ILogSource` (package `Sentinel.LogViewer.Core`) has one member, an observable of the entries. A source that keeps entries sends the ones it holds to every new subscriber first, so a viewer that comes back gets the whole log. `CacheTarget` is such a source.

```csharp
public class MySource : ILogSource
{
    private readonly ReplaySubject<LogEntry> _entries = new(bufferSize: 1000);

    public IObservable<LogEntry> Entries => _entries;

    public void Add(LogEntry entry) => _entries.OnNext(entry);
}
```

```csharp
LogViewer1.Source = new MySource();
```

### Subscription (StartListen/StopListen)

The viewer subscribes to its source every time it is loaded and ends the subscription when it is unloaded, so it keeps working when a tab or a docking panel takes it out of the window and puts it back ([Issue #109](https://github.com/boexler/NLogViewer/issues/109)). While `Pause` is on it does not listen. Each time it subscribes it shows what the source holds, without showing entries twice.

`StartListen()` and `StopListen()` are there for the cases that the viewer cannot see, e.g. a docking system that keeps the control in the window but hides it. They are safe to call several times, and `StartListen()` does nothing when the entries come from an `ItemsSource`.

```csharp
logViewer.StopListen();   // e.g. when the panel is hidden
logViewer.StartListen();  // and when it is shown again
```

## Samples

### open on a new window

![LogViewer][p4]

Create a new `Window` and add a default `LogViewer`

```csharp
<dj:LogViewer TargetName="target1"/>
```

Open the new `Window`

```csharp
TestPopup popup = new TestPopup();
popup.Show();
```

### seperate logger for a task

![LogViewer][p5]

Below is a sample how you could create a `LogViewer` for a task

```csharp
// create unique target name
var taskNumber = _RandomTaskCounter++;
string targetName = $"task{taskNumber}";
// create a unique logger
var loggerName = $"MyFoo.Logger.{taskNumber}";
var logger = LogManager.GetLogger(loggerName);

// create new CacheTarget
CacheTarget target = new CacheTarget
{
    Name = targetName
};

// get config // https://stackoverflow.com/a/3603571/6229375
var config = LogManager.Configuration;

// add target
config.AddTarget(targetName, target);

// create a logging rule for the new logger
LoggingRule loggingRule = new LoggingRule(loggerName, LogLevel.Trace, target);

// add the logger to the existing configuration
config.LoggingRules.Add(loggingRule);

// reassign config back to NLog
LogManager.Configuration = config;

// create a new LogViewer Control with the unique logger target name
LogViewer logViewer = new LogViewer
{
    TargetName = targetName,
};

// add it to the tab control
var tabItem = new TabItem { Header = $"Task {taskNumber}", Content = logViewer };
TabControl1.Items.Add(tabItem);
TabControl1.SelectedItem = tabItem;

// create task which produces some output
var task = new Task(async () =>
{
    while (true)
    {
        logger.Info($"Hello from task nr. {taskNumber}. It's {DateTime.Now.ToLongTimeString()}");
        await Task.Delay(1000);
    }
});
```

## Why CacheTarget?

There is already a `LogViewerTarget`, which is used for [Sentinel][1]. See [here][3]

```xml
<target 
    xsi:type="LogViewer"
    name="sentinel"
    address="udp://127.0.0.1:9999"/>
```

## Contributors

Feel free to make a PullRequest or open an Issue to extend this library!