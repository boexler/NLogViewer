using System.Reactive.Linq;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.TestApp.ControlTest;

internal static class FilteredSource
{
    /// <summary>
    /// A source with the entries of the default buffer that match, so that a viewer can show a part of the log. It follows the buffer
    /// as long as the application runs.
    /// </summary>
    public static ILogSource Create(Func<LogEntry, bool> match)
    {
        var buffer = new LogBuffer(1000);
        LogBuffer.Default.Entries.Where(match).Subscribe(buffer.Add);
        return buffer;
    }
}
