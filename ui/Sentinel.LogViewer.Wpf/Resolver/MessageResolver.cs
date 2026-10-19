using System.Text;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.Resolver
{
    public class MessageResolver : ILogEntryResolver
    {
        public string Resolve(LogEntry logEntry)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(logEntry.Message);

            if (logEntry.Exception != null)
            {
                builder.AppendLine().Append(logEntry.Exception);
            }

            return builder.ToString();
        }
    }
}
