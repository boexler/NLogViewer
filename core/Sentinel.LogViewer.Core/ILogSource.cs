namespace Sentinel.LogViewer.Core;

/// <summary>
/// Where the entries of a log viewer come from: the log of an application (NLog, Microsoft.Extensions.Logging), the messages of a
/// network port, or anything else that produces <see cref="LogEntry"/> objects
/// </summary>
public interface ILogSource
{
	/// <summary>
	/// The entries. A source that keeps entries (a cache) sends the ones it still holds to every new subscriber before it
	/// sends new ones, so a viewer that subscribes later, or again after it was away, gets the whole log.
	/// </summary>
	IObservable<LogEntry> Entries { get; }
}
