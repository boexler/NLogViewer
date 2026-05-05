using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Sentinel.NLogViewer.App.ViewModels;

namespace Sentinel.NLogViewer.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

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
        /// Accept file drops onto the tab area.
        /// </summary>
        private void LogTabsArea_PreviewDragOver(object sender, DragEventArgs e)
        {
	        e.Handled = true;
	        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
		        ? DragDropEffects.Copy
		        : DragDropEffects.None;
        }

        /// <summary>
        /// Imports dropped paths using the same flow as Open File.
        /// </summary>
        private void LogTabsArea_Drop(object sender, DragEventArgs e)
        {
	        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
		        return;

	        if (e.Data.GetData(DataFormats.FileDrop) is not string[] rawPaths)
		        return;

	        var paths = rawPaths.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
	        if (paths.Length == 0)
		        return;

	        _viewModel.BeginImportPathsFromUi(paths);
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
	        using var scope = App.ServiceProvider.CreateScope();
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

