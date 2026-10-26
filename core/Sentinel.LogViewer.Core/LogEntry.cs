namespace Sentinel.LogViewer.Core;

/// <summary>
/// The severity of a log entry. The values are ordered, so levels can be compared: <c>entry.Level >= LogEntryLevel.Error</c>.
/// </summary>
public enum LogEntryLevel
{
	Trace,
	Debug,
	Info,
	Warn,
	Error,
	Fatal
}

/// <summary>
/// One entry of a log, independent of the library that wrote it (NLog, Microsoft.Extensions.Logging, a Log4J message or a line of a file)
/// </summary>
public sealed class LogEntry
{
	private static long _lastId;

	private static readonly IReadOnlyDictionary<string, object?> NoProperties = new Dictionary<string, object?>();

	public LogEntry(LogEntryLevel level, string? loggerName, string? message)
	{
		Level = level;
		LoggerName = loggerName ?? string.Empty;
		Message = message ?? string.Empty;
		Id = Interlocked.Increment(ref _lastId);
	}

	/// <summary>
	/// A copy of the entry with another time stamp, with the same number
	/// </summary>
	public LogEntry WithTimeStamp(DateTime timeStamp) => new(Level, LoggerName, Message)
	{
		TimeStamp = timeStamp,
		Exception = Exception,
		Properties = Properties,
		Id = Id
	};

	public LogEntryLevel Level { get; }

	public string LoggerName { get; }

	/// <summary>
	/// The message as it is shown, with its parameters filled in
	/// </summary>
	public string Message { get; }

	public DateTime TimeStamp { get; init; } = DateTime.Now;

	/// <summary>
	/// The exception of the entry as text, including its stack trace; null when there is none
	/// </summary>
	public string? Exception { get; init; }

	/// <summary>
	/// Further values of the entry, e.g. the thread, the class or the fields of a JSON line
	/// </summary>
	public IReadOnlyDictionary<string, object?> Properties { get; init; } = NoProperties;

	/// <summary>
	/// Number of the entry. Entries get a growing number when they are created, a source can give its own, e.g. the line of a file.
	/// </summary>
	public long Id { get; init; }
}
