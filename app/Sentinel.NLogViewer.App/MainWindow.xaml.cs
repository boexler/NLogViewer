using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Sentinel.NLogViewer.App.Models;
using Sentinel.NLogViewer.App.ViewModels;

namespace Sentinel.NLogViewer.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        private Point _logTabDragStart;
        private LogTabViewModel? _logTabDragSource;

        public MainWindow(MainViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            DataContext = _viewModel;

            // Set window title with version
            SetWindowTitleWithVersion();

            // Setup keyboard shortcuts
            this.InputBindings.Add(new KeyBinding(_viewModel.OpenFileCommand,
                new KeyGesture(Key.O, ModifierKeys.Control)));
            this.InputBindings.Add(new KeyBinding(_viewModel.OpenSettingsCommand,
                new KeyGesture(Key.OemComma, ModifierKeys.Control)));

#if DEBUG
            AddDebugMenu();
#endif
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
        /// Sets the window title with the application version
        /// </summary>
        private void SetWindowTitleWithVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var versionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            
            string version = versionAttribute?.InformationalVersion 
                ?? assembly.GetName().Version?.ToString() 
                ?? "Unknown";
            
            this.Title = $"NLogViewer Client Application - {version}";
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
            _viewModel?.Dispose();
            base.OnClosed(e);
        }
    }
}

