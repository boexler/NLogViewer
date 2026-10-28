using Sentinel.LogViewer.Core;
using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Theming;
using Sentinel.LogViewer.Services.Updates;
using Sentinel.LogViewer.ViewModels;

namespace Sentinel.LogViewer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly SingleInstanceService _singleInstanceService = new();
        private IHost? _host;

        public App()
        {
            if (!_singleInstanceService.IsPrimaryInstance)
                return;

            // Build the host with dependency injection
            var hostBuilder = Host.CreateApplicationBuilder();

            // Register services
            ConfigureServices(hostBuilder.Services);

            _host = hostBuilder.Build();

            // Initialize localization service BEFORE XAML is loaded
            // This ensures the correct culture is set for resource loading
            var localizationService = _host.Services.GetRequiredService<LocalizationService>();

            // The control has its own texts; it follows the language of the application, also when that is changed later.
            localizationService.SetsLanguageOfControl = true;
            localizationService.Initialize();
        }

        /// <summary>
        /// Configures the dependency injection container
        /// </summary>
        private void ConfigureServices(IServiceCollection services)
        {
            // Register services as singletons
            services.AddSingleton<ConfigurationService>();
            services.AddSingleton<LocalizationService>();
            services.AddSingleton<TextFileFormatDetector>();
            services.AddSingleton<TextFileFormatConfigService>();
            services.AddSingleton<UpdateService>();
            services.AddSingleton<ChangelogService>();
            services.AddSingleton<ApplicationTabService>();
            services.AddSingleton<SettingsTransferService>();
            services.AddSingleton<ThemeService>();
            services.AddSingleton<ITrayIcon, NotifyIconTray>();

            // Register services as scoped (one per window/view)
            services.AddScoped<LogReceiverService>();
            services.AddScoped<LogFileParserService>();

            // Register parsers as transient (new instance each time)
            services.AddTransient<Core.Log4JEventParser>();
            services.AddTransient<Parsers.PlainTextParser>();
            services.AddTransient<Parsers.JsonLogParser>();

            // Register ViewModels as scoped
            services.AddScoped<MainViewModel>();
            services.AddScoped<SettingsViewModel>();
            services.AddScoped<LanguageSelectionViewModel>();
            services.AddTransient<AboutViewModel>();

            // Register Windows as transient (new window each time)
            services.AddTransient<MainWindow>();
            services.AddTransient<SettingsWindow>();
            services.AddTransient<LanguageSelectionWindow>();
            services.AddTransient<AboutWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (!_singleInstanceService.IsPrimaryInstance)
            {
                ForwardInvocationAndExit(e.Args);
                return;
            }

            // Create and show the main window using DI
            // Create a scope for the main window and its dependencies
            if (_host != null)
            {
                // The colours are resources, so they must be set before the first window loads.
                var themeService = _host.Services.GetRequiredService<ThemeService>();
                themeService.Apply(_host.Services.GetRequiredService<ConfigurationService>().LoadConfiguration());
                themeService.StartWatching();

                var scope = _host.Services.CreateScope();
                var mainWindow = scope.ServiceProvider.GetRequiredService<MainWindow>();
                MainWindow = mainWindow;

                // Store the scope so it's disposed when the window closes
                mainWindow.Closed += (s, args) => scope.Dispose();

                _singleInstanceService.InvocationReceived += (_, arguments) =>
                    Dispatcher.BeginInvoke(() => mainWindow.HandleExternalInvocation(arguments));
                _singleInstanceService.StartListening();

                if (e.Args.Length > 0)
                    mainWindow.Loaded += (_, _) => mainWindow.HandleExternalInvocation(e.Args);

                mainWindow.Show();
            }
        }

        /// <summary>
        /// Forwards a secondary process invocation to the primary process and exits.
        /// </summary>
        private void ForwardInvocationAndExit(string[] arguments)
        {
            try
            {
                _singleInstanceService.ForwardInvocationAsync(arguments).GetAwaiter().GetResult();
            }
            catch (IOException ex)
            {
                MessageBox.Show(
                    $"The running Sentinel.LogViewer instance could not be reached.{Environment.NewLine}{ex.Message}",
                    "Sentinel.LogViewer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (TimeoutException ex)
            {
                MessageBox.Show(
                    $"The running Sentinel.LogViewer instance did not respond.{Environment.NewLine}{ex.Message}",
                    "Sentinel.LogViewer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Dispose the host and all registered services
            _host?.Dispose();
            _singleInstanceService.Dispose();
            base.OnExit(e);
        }

        /// <summary>
        /// Gets the service provider for dependency injection
        /// </summary>
        public static IServiceProvider? ServiceProvider => (Current as App)?._host?.Services;
    }
}