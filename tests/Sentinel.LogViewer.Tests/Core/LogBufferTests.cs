using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Sentinel.LogViewer.Core;
using Xunit;

namespace Sentinel.LogViewer.Tests.Core;

/// <summary>
/// Tests for the parts of the core that have no user interface: the buffer of entries, the commands, and that the core needs no WPF
/// </summary>
public class LogBufferTests
{
	private static LogEntry Entry(string message) => new(LogEntryLevel.Info, "Logger", message);

	private static string[] Received(LogBuffer buffer, Action<List<LogEntry>>? whileSubscribed = null)
	{
		var received = new List<LogEntry>();
		using var subscription = buffer.Entries.Subscribe(new ListObserver(received));
		whileSubscribed?.Invoke(received);
		return received.Select(entry => entry.Message).ToArray();
	}

	private sealed class ListObserver(List<LogEntry> list) : IObserver<LogEntry>
	{
		public void OnNext(LogEntry value) => list.Add(value);

		public void OnError(Exception error) { }

		public void OnCompleted() { }
	}

	[Fact]
	public void ANewSubscriber_GetsTheHeldEntriesFirst_ThenTheNewOnes_InOrder()
	{
		var buffer = new LogBuffer();
		buffer.Add(Entry("one"));
		buffer.Add(Entry("two"));

		var received = Received(buffer, list => buffer.Add(Entry("three")));

		Assert.Equal(new[] { "one", "two", "three" }, received);
	}

	[Fact]
	public void TheOldestEntriesGo_WhenTheBufferIsFull()
	{
		var buffer = new LogBuffer(capacity: 3);
		for (var i = 1; i <= 5; i++)
			buffer.Add(Entry("entry " + i));

		Assert.Equal(3, buffer.Count);
		Assert.Equal(new[] { "entry 3", "entry 4", "entry 5" }, Received(buffer));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-5)]
	public void WithoutAUsableCapacity_TheBufferHoldsTheDefaultNumber(int capacity)
	{
		Assert.Equal(LogBuffer.DefaultCapacity, new LogBuffer(capacity).Capacity);
	}

	[Fact]
	public void AnEnded_Subscription_GetsNothingMore()
	{
		var buffer = new LogBuffer();
		var received = new List<LogEntry>();
		var subscription = buffer.Entries.Subscribe(new ListObserver(received));
		buffer.Add(Entry("before"));

		subscription.Dispose();
		buffer.Add(Entry("after"));

		Assert.Equal(new[] { "before" }, received.Select(entry => entry.Message));
	}

	[Fact]
	public void Clearing_EmptiesTheBuffer_ButNotWhatSubscribersGot()
	{
		var buffer = new LogBuffer();
		var received = new List<LogEntry>();
		using var subscription = buffer.Entries.Subscribe(new ListObserver(received));
		buffer.Add(Entry("kept by the subscriber"));

		buffer.Clear();

		Assert.Equal(0, buffer.Count);
		Assert.Single(received);
		Assert.Empty(Received(buffer));
	}

	[Fact]
	public async Task EntriesThatComeFromSeveralThreads_AllArrive()
	{
		var buffer = new LogBuffer(capacity: 10_000);
		var received = new List<LogEntry>();
		var gate = new object();
		using var subscription = buffer.Entries.Subscribe(new LockedObserver(received, gate));

		await Task.WhenAll(Enumerable.Range(0, 8).Select(thread => Task.Run(() =>
		{
			for (var i = 0; i < 500; i++)
				buffer.Add(Entry($"{thread}-{i}"));
		})));

		Assert.Equal(4000, buffer.Count);
		lock (gate)
			Assert.Equal(4000, received.Count);
	}

	private sealed class LockedObserver(List<LogEntry> list, object gate) : IObserver<LogEntry>
	{
		public void OnNext(LogEntry value)
		{
			lock (gate)
				list.Add(value);
		}

		public void OnError(Exception error) { }

		public void OnCompleted() { }
	}

	[Fact]
	public void ACommand_RunsItsAction_AlsoWithAParameter()
	{
		var calls = new List<string>();
		var plain = new DelegateCommand(() => calls.Add("plain"));
		var typed = new DelegateCommand<string>(value => calls.Add("typed " + value));

		plain.Execute(null);
		typed.Execute("one");
		typed.Execute(42);

		Assert.True(plain.CanExecute(null));
		Assert.Equal(new[] { "plain", "typed one", "typed " }, calls);
	}

	[Fact]
	public void TheCore_DoesNotNeedWpf()
	{
		var references = typeof(LogEntry).Assembly.GetReferencedAssemblies().Select(name => name.Name).ToList();

		Assert.DoesNotContain("PresentationFramework", references);
		Assert.DoesNotContain("PresentationCore", references);
		Assert.DoesNotContain("WindowsBase", references);
		Assert.DoesNotContain("System.Windows.Forms", references);
	}
}
