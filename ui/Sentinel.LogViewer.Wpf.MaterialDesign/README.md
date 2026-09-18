# Sentinel.LogViewer.Wpf.MaterialDesign

A Material Design themed version of the LogViewer WPF control that provides a modern, beautiful interface for viewing NLog log entries in WPF applications.

## Overview

Sentinel.LogViewer.Wpf.MaterialDesign extends the base LogViewer control with Material Design styling, providing:
- Modern Material Design UI components
- Beautiful log level icons and color coding
- Responsive layout with Material Design cards
- Consistent theming with Material Design principles
- Enhanced search functionality with Material Design chips
- Improved column headers with reduced height and separators

## Installation

### NuGet Package
```xml
<PackageReference Include="Sentinel.LogViewer.Wpf.MaterialDesign" Version="[version]" />
```

### Dependencies
- .NET 8.0 or higher
- MaterialDesignThemes (4.9.0 or higher)
- Sentinel.LogViewer.Wpf (base control)

## Quick Start

### 1. Add Required References

Add the following references to your project:

```xml
<PackageReference Include="MaterialDesignThemes" Version="4.9.0" />
<PackageReference Include="Sentinel.LogViewer.Wpf.MaterialDesign" Version="[version]" />
```

### 2. Configure Application Resources

In your `App.xaml`, add the Material Design theme and LogViewer Material Design styles:

```xml
<Application x:Class="YourApp.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
             xmlns:xamlConverter="clr-namespace:Sentinel.LogViewer.Wpf.XamlConverter;assembly=Sentinel.LogViewer.Wpf"
             xmlns:xamlMultiValueConverter="clr-namespace:Sentinel.LogViewer.Wpf.XamlMultiValueConverter;assembly=Sentinel.LogViewer.Wpf"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <!-- Material Design Theme -->
                <materialDesign:BundledTheme BaseTheme="Light" PrimaryColor="Orange" SecondaryColor="Orange"/>
                <ResourceDictionary Source="pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml" />
                <!-- LogViewer Material Design Styles -->
                <ResourceDictionary Source="pack://application:,,,/Sentinel.LogViewer.Wpf.MaterialDesign;component/Themes/MaterialDesign.LogViewer.xaml"/>
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

## Features

### Material Design Styling
- **Cards**: Log entries are displayed in Material Design cards
- **Icons**: Log levels are represented with Material Design icons
- **Colors**: Consistent color scheme following Material Design guidelines
- **Typography**: Material Design typography

### Search Interface
- **Search Chips**: Search terms are displayed as Material Design chips
- **Regex Indicators**: Regex search terms are prefixed with "/" for identification
- **Context Menus**: Right-click search chips for edit/remove options
- **Search Highlighting**: Matched text is highlighted with Material Design colors

### Layout
- **Reduced Header Height**: Column headers have a reduced height (32px)
- **Column Separators**: Visual separators between columns
- **Responsive Design**: Layout adapts to different screen sizes

### Theme Integration
- **Color Binding**: Search term chips bind to LogViewer color dependency properties
- **Consistent Styling**: All UI elements follow Material Design principles
- **Dark/Light Theme Support**: Compatible with Material Design theme switching

## Customization

### Color Customization
You can customize the appearance by binding to LogViewer's color properties:

```xaml
<dj:LogViewer 
    TraceBackground="{DynamicResource MaterialDesignChipBackground}"
    DebugBackground="{DynamicResource MaterialDesignChipBackground}"
    InfoBackground="{DynamicResource MaterialDesignChipBackground}"
    WarnBackground="{DynamicResource MaterialDesignChipBackground}"
    ErrorBackground="{DynamicResource MaterialDesignChipBackground}"
    FatalBackground="{DynamicResource MaterialDesignChipBackground}"
    SearchHighlightBackground="{DynamicResource MaterialDesignSelection}"
    ShowControlButtons="True"
    ShowFilterButtons="True" />
```

### Search Functionality
The Material Design theme provides enhanced search experience:

```csharp
// Enable regex search mode
nLogViewer.UseRegexSearch = true;

// Add search terms programmatically
nLogViewer.CurrentSearchText = "error";
nLogViewer.AddSearchTerm();

// Customize search highlight color
nLogViewer.SearchHighlightBackground = new SolidColorBrush(Colors.Yellow);
```

## Test Applications

This package includes test applications:

- **Sentinel.LogViewer.Wpf.MaterialDesign.TestApp** - Standalone test application
- **Sentinel.LogViewer.Wpf.TestApp** - Base test application for comparison

## Requirements

- .NET 8.0 or higher
- MaterialDesignThemes 4.9.0 or higher
- LogViewer base control

