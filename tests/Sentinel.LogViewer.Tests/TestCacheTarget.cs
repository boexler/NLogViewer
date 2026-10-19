using System.Reactive.Linq;
using System.Reactive.Subjects;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf.Targets;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Test double for ICacheTarget. Exposes an observable cache and allows pre-filling events
/// so that subscribers (e.g. LogViewer) receive them when they subscribe (ReplaySubject).
/// </summary>
public class TestCacheTarget : ICacheTarget
{
	private readonly ReplaySubject<LogEntry> _subject = new ReplaySubject<LogEntry>(bufferSize: 1000);

	/// <inheritdoc />
	public IObservable<LogEntry> Cache => _subject.AsObservable();

	/// <summary>
	/// Pushes a single log event into the cache. Subscribers receive it; late subscribers get it replayed.
	/// </summary>
	public void Add(LogEntry logEvent)
	{
		_subject.OnNext(logEvent);
	}

	/// <summary>
	/// Pushes multiple log events into the cache. Subscribers receive them; late subscribers get them replayed.
	/// </summary>
	public void AddRange(IEnumerable<LogEntry> logEvents)
	{
		if (logEvents == null) return;
		foreach (var logEvent in logEvents)
			_subject.OnNext(logEvent);
	}
}
