using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentinel.NLogViewer.App.Services;
using Sentinel.NLogViewer.App.ViewModels;

namespace Sentinel.NLogViewer.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly SingleInstanceService _singleInstanceService = new();
        private IHost? _host;

#if DEBUG
        private TestLoggingService? _testLoggingService;
#endif

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

            // Register services as scoped (one per window/view)
            services.AddScoped<UdpLogReceiverService>();
            services.AddScoped<LogFileParserService>();

            // Register parsers as transient (new instance each time)
            services.AddTransient<Parsers.Log4JEventParser>();
            services.AddTransient<Parsers.PlainTextParser>();
            services.AddTransient<Parsers.JsonLogParser>();

            // Register ViewModels as scoped
            services.AddScoped<MainViewModel>();
            services.AddScoped<SettingsViewModel>();
            services.AddScoped<LanguageSelectionViewModel>();

            // Register Windows as transient (new window each time)
            services.AddTransient<MainWindow>();
            services.AddTransient<SettingsWindow>();
            services.AddTransient<LanguageSelectionWindow>();

#if DEBUG
            services.AddSingleton<TestLoggingService>();
            services.AddTransient<TestLoggingViewModel>();
            services.AddTransient<TestLoggingWindow>();
#endif
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

#if DEBUG
                _testLoggingService = _host.Services.GetRequiredService<TestLoggingService>();
                if (ShouldAutoStartTestLogging())
                {
                    _testLoggingService.Start(new Sentinel.NLogViewer.TestLogging.TestLoggingOptions
                    {
                        TargetName = "chainsaw",
                        UdpHost = "127.0.0.1",
                        UdpPort = 4000,
                        MessageIntervalMs = 1000,
                        ExceptionProbability = 0.2
                    });
                }
#endif
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
                    $"The running Sentinel.NLogViewer instance could not be reached.{Environment.NewLine}{ex.Message}",
                    "Sentinel.NLogViewer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (TimeoutException ex)
            {
                MessageBox.Show(
                    $"The running Sentinel.NLogViewer instance did not respond.{Environment.NewLine}{ex.Message}",
                    "Sentinel.NLogViewer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                Shutdown();
            }
        }

#if DEBUG
        private bool ShouldAutoStartTestLogging()
        {
            if (_host == null) return false;
            try
            {
                var configService = _host.Services.GetRequiredService<ConfigurationService>();
                return configService.LoadConfiguration().AutoStartTestLogging;
            }
            catch
            {
                return false;
            }
        }
#endif

        protected override void OnExit(ExitEventArgs e)
        {
#if DEBUG
            _testLoggingService?.Stop();
#endif
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