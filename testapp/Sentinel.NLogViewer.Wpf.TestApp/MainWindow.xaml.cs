using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using Sentinel.NLogViewer.Wpf;
using Sentinel.NLogViewer.Wpf.Resolver;
using Sentinel.NLogViewer.Wpf.Targets;
using NLog;
using NLog.Config;
using NLog.Filters;

namespace Sentinel.NLogViewer.Wpf.TestApp
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public const string LOREM_IPSUM = @"Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";

        private readonly Logger _Logger = LogManager.GetCurrentClassLogger();
        private readonly Logger _Logger2 = LogManager.GetLogger("Lorem.Ipsum.Foo.Hello.World.Lorem.Ipsum");

        private int _CntMessage;
        private int _CntError;


        public MainWindow()
        {
            Title = $"NLogViewer TestApp v{Assembly.GetEntryAssembly()?.GetName().Version} - framework v{AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName}";
            InitializeComponent();
            DataContext = this;
            
            NLogViewer1.TimeStampResolver = new FooTimeStampResolver();
            
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
                        _Logger.Trace($"Hello everyone: {_CntMessage}");
                        break;
                    case 2:
                        _CntMessage++;
                        if (stopwatch.Elapsed.Seconds > 5)
                        {
                            _Logger2.Debug($"Hello everyone: {_CntMessage}");
                        }
                        else
                        {
                            _Logger.Debug($"Hello everyone: {_CntMessage}");
                        }

                        break;
                    case 3:
                        _CntMessage++;
                        _Logger.Info($"Hello everyone: {_CntMessage}");
                        break;
                    case 4:
                        _CntError++;
                        _Logger.Warn($"Hello everyone: {_CntError}");
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
                            _Logger.Error(ex, $"There was an error on divison :/ {_CntError}");
                        }

                        break;
                    case 6:
                        _CntError++;
                        _Logger.Fatal($"{_CntError}\n{LOREM_IPSUM}");
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
            return task;
        }
    }

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
}