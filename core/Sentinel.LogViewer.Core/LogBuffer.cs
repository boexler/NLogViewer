namespace Sentinel.LogViewer.Core;

/// <summary>
/// Keeps the last entries of a log in memory and sends them to everyone who subscribes, followed by the entries that come later. When the
/// buffer is full the oldest entry goes. It is the cache behind a viewer that can come and go, and the memory of a log server.
/// </summary>
public sealed class LogBuffer : ILogSource
{
	/// <summary>
	/// The number of entries that a buffer holds when none is given
	/// </summary>
	public const int DefaultCapacity = 500;

	private static volatile LogBuffer _default = new();

	/// <summary>
	/// The buffer that a viewer shows when it has neither an items source nor a source of its own, and that <c>AddLogViewer()</c> of the
	/// logging of .NET writes to. Set it (or use the capacity option of <c>AddLogViewer</c>) before the viewer is loaded.
	/// </summary>
	public static LogBuffer Default
	{
		get => _default;
		set => _default = value ?? throw new ArgumentNullException(nameof(value));
	}

	private readonly object _gate = new();
	private readonly Queue<LogEntry> _entries = new();
	private readonly List<Subscription> _subscribers = new();

	public LogBuffer(int capacity = DefaultCapacity)
	{
		Capacity = capacity > 0 ? capacity : DefaultCapacity;
	}

	/// <summary>
	/// The most entries that the buffer holds
	/// </summary>
	public int Capacity { get; }

	/// <summary>
	/// The number of entries in the buffer
	/// </summary>
	public int Count
	{
		get
		{
			lock (_gate)
				return _entries.Count;
		}
	}

	public IObservable<LogEntry> Entries => new BufferObservable(this);

	/// <summary>
	/// Adds an entry and sends it to the subscribers
	/// </summary>
	public void Add(LogEntry entry)
	{
		ArgumentNullException.ThrowIfNull(entry);

		Subscription[] subscribers;
		lock (_gate)
		{
			_entries.Enqueue(entry);
			while (_entries.Count > Capacity)
				_entries.Dequeue();

			subscribers = _subscribers.ToArray();
		}

		foreach (var subscriber in subscribers)
			subscriber.Observer.OnNext(entry);
	}

	/// <summary>
	/// Removes all entries from the buffer; the subscribers keep what they got
	/// </summary>
	public void Clear()
	{
		lock (_gate)
			_entries.Clear();
	}

	private IDisposable Subscribe(IObserver<LogEntry> observer)
	{
		var subscription = new Subscription(this, observer);

		// The held entries go out before the lock is let go, so an entry added at the same time reaches the new subscriber after them.
		lock (_gate)
		{
			foreach (var entry in _entries)
				observer.OnNext(entry);

			_subscribers.Add(subscription);
		}

		return subscription;
	}

	private void Unsubscribe(Subscription subscription)
	{
		lock (_gate)
			_subscribers.Remove(subscription);
	}

	private sealed class BufferObservable(LogBuffer buffer) : IObservable<LogEntry>
	{
		public IDisposable Subscribe(IObserver<LogEntry> observer) => buffer.Subscribe(observer);
	}

	private sealed class Subscription(LogBuffer buffer, IObserver<LogEntry> observer) : IDisposable
	{
		public IObserver<LogEntry> Observer { get; } = observer;

		public void Dispose() => buffer.Unsubscribe(this);
	}
}
