using System.Reactive.Linq;
using System.Reactive.Subjects;
using NLog;
using Sentinel.NLogViewer.Wpf.Targets;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Test double for ICacheTarget. Exposes an observable cache and allows pre-filling events
/// so that subscribers (e.g. NLogViewer) receive them when they subscribe (ReplaySubject).
/// </summary>
public class TestCacheTarget : ICacheTarget
{
	private readonly ReplaySubject<LogEventInfo> _subject = new ReplaySubject<LogEventInfo>(bufferSize: 1000);

	/// <inheritdoc />
	public IObservable<LogEventInfo> Cache => _subject.AsObservable();

	/// <summary>
	/// Pushes a single log event into the cache. Subscribers receive it; late subscribers get it replayed.
	/// </summary>
	public void Add(LogEventInfo logEvent)
	{
		_subject.OnNext(logEvent);
	}

	/// <summary>
	/// Pushes multiple log events into the cache. Subscribers receive them; late subscribers get them replayed.
	/// </summary>
	public void AddRange(IEnumerable<LogEventInfo> logEvents)
	{
		if (logEvents == null) return;
		foreach (var logEvent in logEvents)
			_subject.OnNext(logEvent);
	}
}
