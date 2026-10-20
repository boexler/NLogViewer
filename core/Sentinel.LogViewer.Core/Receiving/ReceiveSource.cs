using System;
using System.Threading;

namespace Sentinel.LogViewer.Core;

/// <summary>
/// What a source that receives logs is doing
/// </summary>
public enum ReceiveSourceState
{
	/// <summary>It is switched off, or it was not started yet</summary>
	Stopped,

	/// <summary>It receives</summary>
	Listening,

	/// <summary>It could not be started; <see cref="ReceiveSource.Failure"/> says why</summary>
	Failed
}

/// <summary>
/// Why a source could not be started
/// </summary>
public enum ReceiveSourceFailure
{
	None,

	/// <summary>Another process listens on the port; <see cref="ReceiveSource.BlockingProcess"/> says which one when it is known</summary>
	PortInUse,

	Other
}

/// <summary>
/// One place that logs are received from, e.g. a UDP port. The receiver keeps one for every address that is configured, so that the user
/// can see what receives and what does not, and switch each one on and off.
/// </summary>
public sealed class ReceiveSource
{
	private long _entryCount;
	private long _lastEntryTicks;

	internal ReceiveSource(string address, string kind, int port)
	{
		Address = address;
		Kind = kind;
		Port = port;
	}

	/// <summary>The address as it is configured, e.g. <c>udp://0.0.0.0:4000</c></summary>
	public string Address { get; }

	/// <summary>What kind of source it is: <c>UDP</c>; later TCP and files</summary>
	public string Kind { get; }

	/// <summary>The port; for a source that was configured with port 0 it is the one the system chose, once it is listening</summary>
	public int Port { get; internal set; }

	/// <summary>The short name for the user, e.g. <c>UDP 4000</c></summary>
	public string Name => $"{Kind} {Port}";

	public ReceiveSourceState State { get; internal set; }

	public ReceiveSourceFailure Failure { get; internal set; }

	/// <summary>The message of the failure, with the process that uses the port when it is known; null unless the state is <see cref="ReceiveSourceState.Failed"/></summary>
	public string? Error { get; internal set; }

	/// <summary>Name and id of the process that uses the port, e.g. <c>java.exe (PID 4120)</c>; null when it is not known</summary>
	public string? BlockingProcess { get; internal set; }

	/// <summary>How many entries arrived since the source was started</summary>
	public long EntryCount => Interlocked.Read(ref _entryCount);

	/// <summary>When the last entry arrived, or null when none did since the source was started</summary>
	public DateTime? LastEntryUtc
	{
		get
		{
			var ticks = Interlocked.Read(ref _lastEntryTicks);
			return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
		}
	}

	internal void RecordEntry()
	{
		Interlocked.Increment(ref _entryCount);
		Interlocked.Exchange(ref _lastEntryTicks, DateTime.UtcNow.Ticks);
	}

	internal void ResetCounters()
	{
		Interlocked.Exchange(ref _entryCount, 0);
		Interlocked.Exchange(ref _lastEntryTicks, 0);
	}
}
