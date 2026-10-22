using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.ViewModels;

namespace Sentinel.LogViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly ITrayIcon? _tray;

        /// <summary>At most one notification in this time, so a burst of entries does not flood the screen; what arrives meanwhile is announced after it</summary>
        internal TimeSpan TrayNotificationInterval { get; set; } = TimeSpan.FromSeconds(30);

        private readonly DispatcherTimer _trayTimer = new();
        private int _trayAlerts;
        private LogEntryLevel _trayHighest;
        private DateTime _lastTrayNotification = DateTime.MinValue;

        /// <summary>The state to go back to when the window comes back from the tray: minimizing does not remember a maximized window</summary>
        private WindowState _restoreState = WindowState.Normal;

        private Point _logTabDragStart;
        private LogTabViewModel? _logTabDragSource;

        public MainWindow(MainViewModel viewModel, ITrayIcon? tray = null)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _tray = tray;
            InitializeComponent();
            DataContext = _viewModel;

            // Set window title with version
            SetWindowTitleWithVersion();

            // After an update the changelog opens once, when the window is up.
            Loaded += (_, _) => Dispatcher.BeginInvoke(new Action(_viewModel.AnnounceUpdate), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            // Setup keyboard shortcuts
            this.InputBindings.Add(new KeyBinding(_viewModel.OpenFileCommand,
                new KeyGesture(Key.O, ModifierKeys.Control)));
            this.InputBindings.Add(new KeyBinding(_viewModel.OpenSettingsCommand,
                new KeyGesture(Key.OemComma, ModifierKeys.Control)));

            if (_tray != null)
                SetUpTray(_tray);

            this.InputBindings.Add(new KeyBinding(_viewModel.ShowShortcutsCommand, Key.F1, ModifierKeys.None));

            // Zoom of the log list for this session
            this.InputBindings.Add(new KeyBinding(_viewModel.ZoomInCommand, Key.OemPlus, ModifierKeys.Control));
            this.InputBindings.Add(new KeyBinding(_viewModel.ZoomInCommand, Key.OemPlus, ModifierKeys.Control | ModifierKeys.Shift));
            this.InputBindings.Add(new KeyBinding(_viewModel.ZoomInCommand, Key.Add, ModifierKeys.Control));
            this.InputBindings.Add(new KeyBinding(_viewModel.ZoomOutCommand, Key.OemMinus, ModifierKeys.Control));
            this.InputBindings.Add(new KeyBinding(_viewModel.ZoomOutCommand, Key.Subtract, ModifierKeys.Control));
            this.InputBindings.Add(new KeyBinding(_viewModel.ResetFontZoomCommand, Key.D0, ModifierKeys.Control));
            this.InputBindings.Add(new KeyBinding(_viewModel.ResetFontZoomCommand, Key.NumPad0, ModifierKeys.Control));
            PreviewMouseWheel += (_, e) =>
            {
                if (Keyboard.Modifiers != ModifierKeys.Control)
                    return;

                (e.Delta > 0 ? _viewModel.ZoomInCommand : _viewModel.ZoomOutCommand).Execute(null);
                e.Handled = true;
            };

            // Tabs as in a browser
            this.InputBindings.Add(new KeyBinding(_viewModel.CloseCurrentTabCommand, Key.W, ModifierKeys.Control));
            this.InputBindings.Add(new KeyBinding(_viewModel.CloseCurrentTabCommand, Key.F4, ModifierKeys.Control));
            this.InputBindings.Add(new KeyBinding(_viewModel.SelectNextTabCommand, Key.PageDown, ModifierKeys.Control));
            this.InputBindings.Add(new KeyBinding(_viewModel.SelectPreviousTabCommand, Key.PageUp, ModifierKeys.Control));
            this.InputBindings.Add(new KeyBinding(_viewModel.SelectNextTabCommand, Key.Tab, ModifierKeys.Control));
            this.InputBindings.Add(new KeyBinding(_viewModel.SelectPreviousTabCommand, Key.Tab, ModifierKeys.Control | ModifierKeys.Shift));
            for (var number = 1; number <= 9; number++)
            {
                // Not the numpad: Alt and numpad digits enter a character by its code in a text box.
                this.InputBindings.Add(new KeyBinding(_viewModel.SelectTabCommand, Key.D0 + number, ModifierKeys.Alt) { CommandParameter = number });
            }

#if DEBUG
            AddDebugMenu();
#endif
        }

        /// <summary>
        /// Minimizing moves the window to the tray when the settings say so; the tray icon brings it back, tells how receiving is going and
        /// announces new errors
        /// </summary>
        private void SetUpTray(ITrayIcon tray)
        {
            Loaded += (_, _) => _restoreState = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
            _trayTimer.Tick += (_, _) => NotifyTray(tray);
            StateChanged += (_, _) =>
            {
                if (WindowState != WindowState.Minimized)
                {
                    _restoreState = WindowState;
                    return;
                }

                if (!_viewModel.MinimizeToTray)
                    return;

                _trayAlerts = 0;
                _trayHighest = LogEntryLevel.Trace;
                tray.ToolTip = TrayToolTip();
                tray.Show();
                Hide();
            };
            _viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.ReceiveStatusText))
                    tray.ToolTip = TrayToolTip();
            };
            _viewModel.AlertsReceived += (_, alert) =>
            {
                if (!tray.IsVisible)
                    return;

                _trayAlerts += alert.Count;
                _trayHighest = (LogEntryLevel)Math.Max((int)_trayHighest, (int)alert.Highest);
                var wait = TrayNotificationInterval - (DateTime.UtcNow - _lastTrayNotification);
                if (wait <= TimeSpan.Zero)
                {
                    NotifyTray(tray);
                }
                else if (!_trayTimer.IsEnabled)
                {
                    _trayTimer.Interval = wait;
                    _trayTimer.Start();
                }
            };
            tray.RestoreRequested += (_, _) => RestoreFromTray();
            tray.ExitRequested += (_, _) => _viewModel.ExitCommand.Execute(null);
        }

        private void NotifyTray(ITrayIcon tray)
        {
            _trayTimer.Stop();
            if (!tray.IsVisible || _trayAlerts == 0)
                return;

            _lastTrayNotification = DateTime.UtcNow;
            var text = _viewModel.TrayNotificationText(_trayAlerts);
            var isWarning = _trayHighest < LogEntryLevel.Error;
            _trayAlerts = 0;
            _trayHighest = LogEntryLevel.Trace;
            tray.Notify(ProductNames.Display, text, isWarning);
        }

        private string TrayToolTip() => $"{ProductNames.Display} – {_viewModel.ReceiveStatusText}";

        private void RestoreFromTray()
        {
            Show();
            if (WindowState == WindowState.Minimized)
                WindowState = _restoreState;

            Activate();
            _tray?.Hide();
            _trayTimer.Stop();
            _trayAlerts = 0;
            _trayHighest = LogEntryLevel.Trace;
        }

        /// <summary>
        /// Activates this window and imports paths supplied by a shell or secondary-process invocation.
        /// </summary>
        public void HandleExternalInvocation(IReadOnlyList<string> arguments)
        {
            RestoreFromTray();

            if (arguments.Count > 0)
                _viewModel.BeginImportPathsFromUi(arguments);
        }

        /// <summary>
        /// Accept file drops onto the tab area (and tab headers via <see cref="LogTabItem_Drop"/>).
        /// </summary>
        private void LogTabsArea_PreviewDragOver(object sender, DragEventArgs e)
        {
	        if (e.Data.GetDataPresent(DataFormats.FileDrop))
	        {
		        e.Effects = DragDropEffects.Copy;
		        e.Handled = true;
	        }
	        else if (e.Data.GetDataPresent(typeof(LogTabViewModel)))
	        {
		        e.Effects = DragDropEffects.Move;
		        e.Handled = true;
	        }
	        else
	        {
		        e.Effects = DragDropEffects.None;
		        // Do not mark Handled so drag routing can reach tab headers for tab reorder.
	        }
        }

        /// <summary>
        /// Imports dropped paths using the same flow as Open File.
        /// </summary>
        private void LogTabsArea_Drop(object sender, DragEventArgs e)
        {
	        TryHandleFileDrop(e);
        }

        /// <summary>
        /// Imports dropped files when the drop hits a tab header.
        /// </summary>
        private bool TryHandleFileDrop(DragEventArgs e)
        {
	        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
		        return false;

	        if (e.Data.GetData(DataFormats.FileDrop) is not string[] rawPaths)
		        return false;

	        var paths = rawPaths.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
	        if (paths.Length == 0)
		        return false;

	        _viewModel.BeginImportPathsFromUi(paths);
	        e.Handled = true;
	        return true;
        }

        /// <summary>
        /// Opens the menu of a tab below its header, the same menu as with a right-click
        /// </summary>
        private void TabMenuButton_Click(object sender, RoutedEventArgs e)
        {
            DependencyObject? element = sender as DependencyObject;
            while (element != null && element is not TabItem)
                element = VisualTreeHelper.GetParent(element);

            if (element is not TabItem { ContextMenu: { } menu } tabItem)
                return;

            menu.PlacementTarget = tabItem;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
            e.Handled = true;
        }

        /// <summary>
        /// Sets what happens when the application of a tab starts again; the Tag of the menu item names the behaviour
        /// </summary>
        private void RestartBehaviorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { DataContext: LogTabViewModel tab, Tag: string name })
                _viewModel.SetRestartBehavior(tab, Services.ApplicationTabService.ParseRestartBehavior(name));
        }

        /// <summary>
        /// Begins tracking a possible tab-header drag for reordering.
        /// </summary>
        private void LogTabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
	        if (sender is not TabItem tabItem)
		        return;

	        if (tabItem.DataContext is LogTabViewModel vm)
	        {
		        _logTabDragStart = e.GetPosition(null);
		        _logTabDragSource = vm;
	        }
        }

        /// <summary>
        /// A click with the middle mouse button closes the tab, as in a browser.
        /// </summary>
        private void LogTabItem_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && sender is TabItem { DataContext: LogTabViewModel tab })
            {
                _viewModel.CloseTabCommand.Execute(tab);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Clears tab drag tracking when the mouse button is released without starting a drag.
        /// </summary>
        private void LogTabItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
	        _logTabDragSource = null;
        }

        /// <summary>
        /// Starts a tab reorder drag once the cursor moves past the drag threshold.
        /// </summary>
        private void LogTabItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
	        if (_logTabDragSource == null || e.LeftButton != MouseButtonState.Pressed)
		        return;

	        if (_viewModel.LogTabs.Count < 2)
		        return;

	        var pos = e.GetPosition(null);
	        var diff = pos - _logTabDragStart;
	        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
	            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
		        return;

	        if (sender is not TabItem tabItem)
		        return;

	        var data = new DataObject(typeof(LogTabViewModel), _logTabDragSource);
	        DragDrop.DoDragDrop(tabItem, data, DragDropEffects.Move);
	        _logTabDragSource = null;
        }

        /// <summary>
        /// Shows copy vs move cursor when dragging files or tab headers over a tab.
        /// </summary>
        private void LogTabItem_PreviewDragOver(object sender, DragEventArgs e)
        {
	        if (e.Data.GetDataPresent(DataFormats.FileDrop))
	        {
		        e.Effects = DragDropEffects.Copy;
		        e.Handled = true;
	        }
	        else if (e.Data.GetDataPresent(typeof(LogTabViewModel)))
	        {
		        e.Effects = DragDropEffects.Move;
		        e.Handled = true;
	        }
        }

        /// <summary>
        /// Handles file import or tab reorder when dropping onto a tab header.
        /// </summary>
        private void LogTabItem_Drop(object sender, DragEventArgs e)
        {
	        if (TryHandleFileDrop(e))
		        return;

	        if (!e.Data.GetDataPresent(typeof(LogTabViewModel)))
		        return;

	        var source = e.Data.GetData(typeof(LogTabViewModel)) as LogTabViewModel;
	        if (source == null || sender is not TabItem targetItem)
		        return;

	        if (targetItem.DataContext is not LogTabViewModel target)
		        return;

	        var insertBefore = e.GetPosition(targetItem).X < targetItem.ActualWidth * 0.5;
	        _viewModel.MoveTab(source, target, insertBefore);
	        e.Handled = true;
        }

#if DEBUG
        private void AddDebugMenu()
        {
            var testLoggingItem = new MenuItem { Header = "_Test logging" };
            testLoggingItem.Click += (s, _) =>
            {
                using var scope = App.ServiceProvider!.CreateScope();
                var window = scope.ServiceProvider.GetRequiredService<TestLoggingWindow>();
                window.Owner = this;
                window.Show();
            };
            var debugMenu = new MenuItem { Header = "_Debug" };
            debugMenu.Items.Add(testLoggingItem);
            MainMenu.Items.Insert(MainMenu.Items.Count - 1, debugMenu);
        }
#endif

        /// <summary>
        /// Sets the window title with the four-part application version (for example "v4.0.0.0").
        /// </summary>
        private void SetWindowTitleWithVersion()
        {
            // InformationalVersion includes GitVersion build metadata ("+Branch.master.Sha...").
            var version = Assembly.GetExecutingAssembly().GetName().Version;

            this.Title = version is null
                ? ProductNames.Display
                : $"{ProductNames.Display} - v{version}";
        }

        /// <summary>
        /// Opens the language selection window when the language button is clicked
        /// </summary>
        private void LanguageButton_Click(object sender, RoutedEventArgs e)
        {
	        var provider = App.ServiceProvider ?? throw new InvalidOperationException("Service provider is not initialized.");
	        using var scope = provider.CreateScope();
	        var languageWindow = scope.ServiceProvider.GetRequiredService<LanguageSelectionWindow>();
	        var result = languageWindow.ShowDialog(this);

	        if (result == true && !string.IsNullOrEmpty(languageWindow.SelectedLanguageCode))
	        {
		        _viewModel.ChangeLanguageCommand.Execute(languageWindow.SelectedLanguageCode);
	        }
		}

        protected override void OnClosed(System.EventArgs e)
        {
            _tray?.Hide();
            _viewModel?.Dispose();
            base.OnClosed(e);
        }
    }
}

