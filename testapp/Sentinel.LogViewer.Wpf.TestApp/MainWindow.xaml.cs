using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using Sentinel.LogViewer.Wpf.Resolver;
using Microsoft.Extensions.Logging;

namespace Sentinel.LogViewer.Wpf.TestApp
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public const string LOREM_IPSUM = @"Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";

        private readonly ILogger _Logger = App.LoggerFactory.CreateLogger<MainWindow>();
        private readonly ILogger _Logger2 = App.LoggerFactory.CreateLogger("Lorem.Ipsum.Foo.Hello.World.Lorem.Ipsum");

        private int _CntMessage;
        private int _CntError;


        public MainWindow()
        {
            Title = $"LogViewer TestApp v{Assembly.GetEntryAssembly()?.GetName().Version} - framework v{AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName}";
            InitializeComponent();
            DataContext = this;
            
            LogViewer1.TimeStampResolver = new FooTimeStampResolver();
            LogViewer1.Source = App.Filtered(entry => entry.Level <= LogEntryLevel.Info);
            LogViewer2.Source = App.Filtered(entry => entry.Level >= LogEntryLevel.Warn);
            
            Stopwatch stopwatch = Stopwatch.StartNew();
            Random random = new Random();
            var sync = SynchronizationContext.Current ?? new DispatcherSynchronizationContext(Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
            Observable.Interval(TimeSpan.FromMilliseconds(200)).ObserveOn(sync).Subscribe(l =>
            {
                //if((_CntMessage == 10 || _CntError == 20) && TabControl1.Items.Count > 0)
                //    TabControl1.Items.RemoveAt(0);
                switch (random.Next(1, 6))
                {
                    case 1:
                        _CntMessage++;
                        _Logger.LogTrace($"Hello everyone: {_CntMessage}");
                        break;
                    case 2:
                        _CntMessage++;
                        if (stopwatch.Elapsed.Seconds > 5)
                        {
                            _Logger2.LogDebug($"Hello everyone: {_CntMessage}");
                        }
                        else
                        {
                            _Logger.LogDebug($"Hello everyone: {_CntMessage}");
                        }

                        break;
                    case 3:
                        _CntMessage++;
                        _Logger.LogInformation($"Hello everyone: {_CntMessage}");
                        break;
                    case 4:
                        _CntError++;
                        _Logger.LogWarning($"Hello everyone: {_CntError}");
                        break;
                    case 5:
                        _CntError++;

                        try
                        {
                            int a = 0;
                            int b = 1;
                            var c = b / a;
                        }
                        catch (Exception ex)
                        {
                            _Logger.LogError(ex, $"There was an error on divison :/ {_CntError}");
                        }

                        break;
                    case 6:
                        _CntError++;
                        _Logger.LogCritical($"{_CntError}\n{LOREM_IPSUM}");
                        break;
                }
            });
        }

        private void Button_OpenPopup_Click(object sender, RoutedEventArgs e)
        {
            TestPopup popup = new TestPopup();
            popup.Show();
        }

        private void Button_AddTask_Click(object sender, RoutedEventArgs e)
        {
            AddNewTabWithLogger().Start();
        }

        private int _RandomTaskCounter;
        private Task AddNewTabWithLogger()
        {
            var taskNumber = _RandomTaskCounter++;
            // create a unique logger
            var loggerName = $"MyFoo.Logger.{taskNumber}";
            var logger = App.LoggerFactory.CreateLogger(loggerName);

            // create a new LogViewer Control that shows only the entries of this logger
            LogViewer logViewer = new LogViewer
            {
                Source = App.Filtered(entry => entry.LoggerName == loggerName),
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
                    logger.LogInformation($"Hello from task nr. {taskNumber}. It's {DateTime.Now.ToLongTimeString()}");
                    await Task.Delay(1000);
                }
            });
            return task;
        }
    }

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
}