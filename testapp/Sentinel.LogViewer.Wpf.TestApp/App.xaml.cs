using System;
using System.Reactive.Linq;
using System.Windows;
using Microsoft.Extensions.Logging;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Extensions.Logging;

namespace Sentinel.LogViewer.Wpf.TestApp
{
    /// <summary>
    /// Interaktionslogik für "App.xaml"
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// The logging of the test app: everything that is logged goes into <see cref="LogBuffer.Default"/>
        /// </summary>
        public static ILoggerFactory LoggerFactory { get; } = Microsoft.Extensions.Logging.LoggerFactory.Create(
            builder => builder.SetMinimumLevel(LogLevel.Trace).AddLogViewer(options => options.Capacity = 5000));

        /// <summary>
        /// A source with the entries of the default buffer that match, so that a viewer can show a part of the log
        /// </summary>
        public static ILogSource Filtered(Func<LogEntry, bool> match)
        {
            var buffer = new LogBuffer(1000);
            LogBuffer.Default.Entries.Where(match).Subscribe(buffer.Add);
            return buffer;
        }
    }
}
