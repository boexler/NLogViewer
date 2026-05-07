[1]: https://github.com/yarseyah/sentinel
[2]: https://github.com/dojo90/NLogViewer/blob/master/src/NLogViewer/Targets/CacheTarget.cs
[3]: https://github.com/yarseyah/sentinel#nlogs-nlogviewer-target-configuration

[p1]: doc/images/control.png "NLogViewer"
[p2]: doc/images/overview.gif "NLogViewer"
[p3]: doc/images/colors.png "NLogViewer"
[p4]: doc/images/openpopup.gif "NLogViewer"
[p5]: doc/images/newtask.gif "NLogViewer"
[p6]: doc/images/nlogviewercolumns.png "Column Visibility Properties"
[p7]: doc/images/filters1.png "Filter Buttons - All Visible"
[p8]: doc/images/filters2.png "Filter Buttons - Some Hidden"
[p9]: doc/images/controls1.png "Control Buttons - All Visible"
[p10]: doc/images/controls2.png "Control Buttons - Hidden"

[nuget]: https://nuget.org/packages/Sentinel.NLogViewer/

## Nuget

[![NuGet](https://img.shields.io/nuget/v/sentinel.nlogviewer.svg "nuget")](https://www.nuget.org/packages/Sentinel.NLogViewer)
[![NuGetDownloads](https://img.shields.io/nuget/dt/sentinel.nlogviewer.svg "nuget downloads")](https://www.nuget.org/packages/Sentinel.NLogViewer)

A NuGet-package is available [here][nuget].

NLogViewer
==========

NLogViewer is a ui control library to visualize NLog logs in your personal application. It is mainly based on [Sentinel][1] and its controls.

supported Framework: `.NET8`

## Desktop application (Sentinel.NLogViewer.App)

GitHub **Releases** ship:

- **MSI installer** (`NLogViewer-<version>-win-x64.msi`): installs under Program Files with a license dialog (MIT), Start Menu shortcut, and optional features (desktop shortcut, `.log` association) selectable in **Custom** setup. File association for `.nlog` is on by default. Silent install: `msiexec /i NLogViewer-<version>-win-x64.msi /qn` (optional features with higher install level stay off unless you raise `INSTALLLEVEL` or use transforms).
- **Portable ZIP** (self-contained and framework-dependent): unpack and run `Sentinel.NLogViewer.App.exe` from any folder; a `NLogViewer.portable` marker file directs settings to `appsettings.json` beside the app (see `README-Portable.txt` in the ZIP). Installed (MSI) copies keep using per-user `%LocalAppData%\Sentinel.NLogViewer.App\`.

Build the installer locally (Windows): `dotnet build installer/Sentinel.NLogViewer.Installer.wixproj -c Release` — output `installer/bin/Release/NLogViewer.msi`.

![NLogViewer][p2]

## Material Design Theme

This project also includes **NLogViewer.MaterialDesign** - a Material Design theme and style package for the NLogViewer control. The Material Design theme provides a modern, clean UI following Google's Material Design guidelines.

📦 **NLogViewer.MaterialDesign** - [View Project](src/NLogViewer.MaterialDesign/)

## Quick Start

### 1. Add Required References

Add the following NuGet package reference to your project:

```xml
<PackageReference Include="NLogViewer" Version="[version]" />
```

### 2. Configure Application Resources

In your `App.xaml`, add the NLogViewer theme and styles:

```xml
<Application x:Class="YourApp.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:xamlConverter="clr-namespace:Sentinel.NLogViewer.Wpf.XamlConverter;assembly=Sentinel.NLogViewer.Wpf"
             xmlns:xamlMultiValueConverter="clr-namespace:Sentinel.NLogViewer.Wpf.XamlMultiValueConverter;assembly=Sentinel.NLogViewer.Wpf"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <!-- NLogViewer Theme -->
                <ResourceDictionary Source="pack://application:,,,/Sentinel.NLogViewer.Wpf;component/Themes/Generic.xaml"/>
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
        xmlns:dj="clr-namespace:Sentinel.NLogViewer.Wpf;assembly=Sentinel.NLogViewer.Wpf">
    <Grid>
        <dj:NLogViewer />
    </Grid>
</Window>
```

### NLog Configuration

`NLogViewer` is subscribing to [CacheTarget][2]. By default, the `NLogViewer` is automatically creating a [CacheTarget][2] with `loggingPattern  "*"` and `LogLevel "Trace"`.

If you want to customize the `loggingPattern` and `LogLevel`, add the following to your `Nlog.config`.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog 
  xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:schemaLocation="http://www.nlog-project.org/schemas/NLog.xsd NLog.xsd"
  autoReload="true">

  <extensions> 
    <add assembly="NLogViewer"/> 
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

The NLogViewer includes control buttons that allow you to manage log viewing behavior. The control buttons are organized in a GroupBox and can be controlled programmatically.

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
nLogViewer.AutoScroll = true;  // Enable auto-scroll
nLogViewer.AutoScroll = false; // Disable auto-scroll

// Pause/resume logging
nLogViewer.Pause = true;  // Pause logging
nLogViewer.Pause = false; // Resume logging

// Hide the entire control button group
nLogViewer.ShowControlButtons = false;
```

**XAML Binding:**

```xaml
<dj:NLogViewer 
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

![NLogViewer][p3]

### Multi targeting

Use more than one instance of `NLogViewer` to match different `rules`.

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
<dj:NLogViewer TargetName="target1"/>
<dj:NLogViewer TargetName="target2"/>
```

### Column Visibility

Control which columns are visible in the NLogViewer. You can dynamically show or hide individual columns using the provided Dependency Properties.

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
nLogViewer.ShowIdColumn = false;
nLogViewer.ShowLevelColumn = false;

// Show columns again
nLogViewer.ShowIdColumn = true;
nLogViewer.ShowLevelColumn = true;
```

**XAML Binding:**

```xaml
<dj:NLogViewer ShowIdColumn="{Binding IsIdColumnVisible}" ShowLevelColumn="{Binding IsLevelColumnVisible}" />
```

### Filter Buttons

The NLogViewer includes filter buttons that allow you to hide/show specific log levels. The filter buttons are organized in a GroupBox and can be controlled programmatically.

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
nLogViewer.TraceFilter = true;  // Hide Trace entries
nLogViewer.DebugFilter = true;  // Hide Debug entries
nLogViewer.InfoFilter = true;   // Hide Info entries

// Show log levels again
nLogViewer.TraceFilter = false; // Show Trace entries
nLogViewer.DebugFilter = false; // Show Debug entries

// Hide the entire filter button group
nLogViewer.ShowFilterButtons = false;
```

**XAML Binding:**

```xaml
<dj:NLogViewer 
    TraceFilter="{Binding HideTraceLogs}" 
    DebugFilter="{Binding HideDebugLogs}"
    ShowFilterButtons="{Binding ShowFilters}" />
```

**Filter Logic:**
- When a filter property is set to `true`, entries of that log level are **hidden**
- When a filter property is set to `false`, entries of that log level are **shown**
- The filter buttons are ToggleButtons that automatically bind to these properties
- The entire filter group can be hidden using `ShowFilterButtons = false`

### Search Functionality

The NLogViewer includes search capabilities that filter log entries based on text patterns or regular expressions. Search terms are displayed as chips/tags and can be managed through the UI or programmatically.

**Search Features:**
- **Text Search** - Case-insensitive substring matching
- **Regex Search** - Full regular expression pattern matching
- **AND Logic** - All search terms must match for an entry to be displayed
- **Search Highlighting** - Matched text is highlighted in both message and logger name columns
- **Context Menu** - Right-click search terms for edit/remove options

**Search Properties:**
- `CurrentSearchText` - The text currently being typed in the search box
- `UseRegexSearch` - Toggle between text search and regex search modes
- `ActiveSearchTerms` - Collection of active search terms (read-only)
- `SearchHighlightBackground` - Brush used to highlight matched text

**Usage:**

```csharp
// Programmatically add search terms
nLogViewer.CurrentSearchText = "error";
nLogViewer.AddSearchTerm(); // Adds as text search

// Enable regex mode and add regex pattern
nLogViewer.UseRegexSearch = true;
nLogViewer.CurrentSearchText = @"\d{4}-\d{2}-\d{2}";
nLogViewer.AddSearchTerm(); // Adds as regex search

// Remove specific search term
var searchTerm = nLogViewer.ActiveSearchTerms.First();
nLogViewer.RemoveSearchTerm(searchTerm);

// Clear all search terms
nLogViewer.ClearAllSearchTerms();

// Customize search highlight color
nLogViewer.SearchHighlightBackground = Brushes.Yellow;
```

**XAML Binding:**

```xaml
<dj:NLogViewer 
    CurrentSearchText="{Binding SearchText}"
    UseRegexSearch="{Binding IsRegexMode}"
    SearchHighlightBackground="{Binding HighlightBrush}" />
```

**Search Logic:**
- Search terms are applied to both the **message** and **logger name** columns
- **AND logic**: ALL active search terms must match for a log entry to be displayed
- Text search is case-insensitive and performs substring matching
- Regex search uses full .NET regular expression patterns
- Invalid regex patterns show a warning dialog and are not added
- Search highlighting works in real-time as you add/remove terms

### Control Architecture

NLogViewer is built as a CustomControl with theming support:

**Architecture:**
- **CustomControl Base Class** - Enables proper theming and styling
- **RelayCommand System** - MVVM-compatible command system
- **Theme Support** - Full support for custom themes including Material Design
- **Code Organization** - Separation of concerns between UI and logic

### Format output (ILogEventInfoResolver)

To format the output of a `LogEventInfo`, implement a new instance of `ILogEventInfoResolver` and bind it to the `Resolver` you want to customize:

```csharp
/// <summary>
/// Reformat the DateTime
/// </summary>
public class FooTimeStampResolver : ILogEventInfoResolver
{
    public string Resolve(LogEventInfo logEventInfo)
    {
        return logEventInfo.TimeStamp.ToUniversalTime().ToString();
    }
}
```

```csharp
NLogViewer1.TimeStampResolver = new FooTimeStampResolver();
```

### Subscription Management (StartListen/StopListen)

The `NLogViewer` provides manual control over log event subscriptions through the `StartListen()` and `StopListen()` methods. These methods are particularly useful in docking systems or scenarios where the control's lifecycle needs to be managed manually.

**Purpose:**
These methods were implemented to address [Issue #90](https://github.com/dojo90/NLogViewer/issues/90) - subscription disposal when undocking the viewer parent container. The issue occurred when NLogViewer controls were used in docking systems where the control would be moved between different parent windows, causing subscription leaks and improper disposal.

**Root Cause:**
When undocking a control from a docking system, the `Unloaded` event is triggered, which automatically disposes the subscription. This means that after undocking, the control is no longer listening for log events. Therefore, `StartListen()` must always be called in the `DockChanged` event handler to restore the subscription after undocking.

**Method Details:**

#### `StartListen()`
- **Purpose:** Starts listening for log events by subscribing to the cache target
- **When to use:** 
  - When undocking from a docking system
  - When the window loads again after being hidden
  - When resuming log monitoring after pausing
- **Behavior:** 
  - Subscribes to the CacheTarget's observable stream
  - Buffers log events for 100ms intervals for better performance
  - Automatically manages parent window references for proper disposal
  - Prevents duplicate subscriptions if already listening

#### `StopListen()`
- **Purpose:** Stops listening for log events by disposing the subscription
- **When to use:**
  - When docking in a docking system
  - When the window is being unloaded
  - When pausing log monitoring temporarily
- **Behavior:**
  - Disposes the current subscription
  - Clears the listening state
  - Prevents memory leaks from orphaned subscriptions

**Usage Example:**

```csharp
// Manual control in docking scenarios
private void OnDockChanged(object sender, DockChangedEventArgs e)
{
    // Control is being undocked - MUST call StartListen() because 
    // the Unloaded event was triggered and disposed the subscription
    nLogViewer.StartListen();
}

// Pause/Resume functionality
private void ToggleLogging()
{
    if (nLogViewer.IsListening)
    {
        nLogViewer.StopListen();
    }
    else
    {
        nLogViewer.StartListen();
    }
}
```

**Automatic Management:**
The control automatically calls `StartListen()` when loaded and `StopListen()` when properly disposed. The methods are designed to be safe to call multiple times and handle edge cases like design-time mode and missing parent windows.

## Samples

### open on a new window

![NLogViewer][p4]

Create a new `Window` and add a default `NLogViewer`

```csharp
<dj:NLogViewer TargetName="target1"/>
```

Open the new `Window`

```csharp
TestPopup popup = new TestPopup();
popup.Show();
```

### seperate logger for a task

![NLogViewer][p5]

Below is a sample how you could create a `NLogViewer` for a task

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

// create a new NLogViewer Control with the unique logger target name
NLogViewer nLogViewer = new NLogViewer
{
    TargetName = targetName,
};

// add it to the tab control
var tabItem = new TabItem { Header = $"Task {taskNumber}", Content = nLogViewer };
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

There is already a `NLogViewerTarget`, which is used for [Sentinel][1]. See [here][3]

```xml
<target 
    xsi:type="NLogViewer"
    name="sentinel"
    address="udp://127.0.0.1:9999"/>
```

## Contributors

Feel free to make a PullRequest or open an Issue to extend this library!