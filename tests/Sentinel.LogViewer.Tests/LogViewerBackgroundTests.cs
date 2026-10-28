using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Data;
using Sentinel.LogViewer.Core;
using Xunit;
using static Sentinel.LogViewer.Tests.WpfTestHelper;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests that a big list is filtered and searched in the background: the same result as for a small list, a card with the progress after a
/// while, and a cancel that keeps the old result. The number of entries that counts as big is made small for the tests.
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerBackgroundTests
{
	private const int Count = 300;
	private readonly WpfStaContextFixture _context;

	public LogViewerBackgroundTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private void WithBigList(Action<Wpf.LogViewer, ObservableCollection<LogEntry>> test) => _context.RunOnSta(() =>
	{
		var threshold = Wpf.LogViewer.BackgroundThreshold;
		var chunk = Wpf.LogViewer.BackgroundChunk;
		var entries = new ObservableCollection<LogEntry>(Enumerable.Range(0, Count)
			.Select(i => new LogEntry((LogEntryLevel)(i % 6), "Logger" + (i % 5), "message " + i + (i % 7 == 0 ? " seven" : ""))));
		var (window, viewer) = ShowInWindow(entries);
		try
		{
			Wpf.LogViewer.BackgroundThreshold = 50;
			test(viewer, entries);
		}
		finally
		{
			Wpf.LogViewer.BackgroundThreshold = threshold;
			Wpf.LogViewer.BackgroundChunk = chunk;
			Wpf.LogViewer.BackgroundChunkHook = null;
			window.Close();
		}
	});

	private static int Shown(Wpf.LogViewer viewer) => ((CollectionView)viewer.LogEvents.View).Count;

	/// <summary>Lets the dispatcher work (the checks hand their result back to it) until the condition is true</summary>
	private static bool WaitFor(Func<bool> condition, int milliseconds = 5000)
	{
		var end = DateTime.UtcNow.AddMilliseconds(milliseconds);
		while (DateTime.UtcNow < end)
		{
			Settle();
			if (condition())
				return true;

			Thread.Sleep(20);
		}

		return condition();
	}

	[Fact]
	public void ABigList_IsFilteredInTheBackground_WithTheResultOfASmallList_AndLaterEntriesAreJudgedOnArrival() =>
		WithBigList((viewer, entries) =>
		{
			viewer.DebugFilter = true;

			// The list keeps showing everything until the check is done.
			Assert.True(WaitFor(() => Shown(viewer) == Count - Count / 6));
			Assert.DoesNotContain(((CollectionView)viewer.LogEvents.View).Cast<LogEntry>(), entry => entry.Level == LogEntryLevel.Debug);
			Assert.Equal(Count, viewer.VerdictCount);

			// The verdicts were made in the background; entries that come now are judged when the view asks.
			entries.Add(new LogEntry(LogEntryLevel.Debug, "Late", "hidden"));
			entries.Add(new LogEntry(LogEntryLevel.Error, "Late", "shown"));
			Settle();
			Assert.Equal(Count - Count / 6 + 1, Shown(viewer));
			Assert.Equal(Count + 2, viewer.VerdictCount);

			// Entries that leave the list leave the verdicts.
			entries.RemoveAt(0);
			entries.RemoveAt(0);
			Settle();
			Assert.Equal(Count - Count / 6 + 1 - 1, Shown(viewer));
			Assert.Equal(Count, viewer.VerdictCount);

			entries.Clear();
			Settle();
			Assert.Equal(0, viewer.VerdictCount);
			entries.Add(new LogEntry(LogEntryLevel.Error, "Late", "again"));
			Settle();
			Assert.Equal(1, viewer.VerdictCount);
			entries.Add(new LogEntry(LogEntryLevel.Info, "Late", "one more"));

			viewer.DebugFilter = false;
			Assert.True(WaitFor(() => Shown(viewer) == entries.Count));
			// No filter any more: the verdicts are not needed.
			Assert.Equal(0, viewer.VerdictCount);
		});

	[Fact]
	public void AQuickCheck_NeverShowsTheCard()
		=> WithBigList((viewer, _) =>
		{
			viewer.DebugFilter = true;

			var cardShown = false;
			Assert.True(WaitFor(() =>
			{
				cardShown |= viewer.IsBusy;
				return Shown(viewer) == Count - Count / 6;
			}));
			Assert.False(cardShown);
			Assert.False(viewer.IsBusy);
		});

	[Fact]
	public void ASlowCheck_ShowsTheCardWithItsProgress_AndCancelKeepsTheOldResult() =>
		WithBigList((viewer, _) =>
		{
			using var gate = new ManualResetEventSlim();
			Wpf.LogViewer.BackgroundChunk = 10;
			Wpf.LogViewer.BackgroundChunkHook = () => gate.Wait(TimeSpan.FromSeconds(10));

			viewer.DebugFilter = true;

			Assert.True(WaitFor(() => viewer.IsBusy));
			Assert.Equal("Filtering the entries", viewer.BusyText);
			Assert.Matches(@"^10 of 300$", viewer.BusyDetail.Split(" · ")[0]);
			Assert.Equal(Count, Shown(viewer));

			viewer.CancelBusyCommand.Execute(null);
			gate.Set();
			Assert.True(WaitFor(() => !viewer.IsBusy));
			Settle();

			// Nothing was taken over: the list still shows what it showed before.
			Assert.Equal(Count, Shown(viewer));
		});

	[Fact]
	public void ARecountThatNewEntriesAskFor_NeverShowsTheCard() =>
		WithBigList((viewer, entries) =>
		{
			viewer.FindText = "seven";
			Assert.True(WaitFor(() => viewer.FindResultText != string.Empty));

			using var gate = new ManualResetEventSlim();
			var chunks = 0;
			Wpf.LogViewer.BackgroundChunk = 10;
			Wpf.LogViewer.BackgroundChunkHook = () =>
			{
				Interlocked.Increment(ref chunks);
				gate.Wait(TimeSpan.FromSeconds(10));
			};

			// A new entry makes the search count again; nobody asked for it, so the list stays free although the check is slow.
			entries.Add(new LogEntry(LogEntryLevel.Info, "Late", "seven again"));
			var end = DateTime.UtcNow.AddMilliseconds(1000);
			while (DateTime.UtcNow < end)
			{
				Settle();
				Assert.False(viewer.IsBusy);
				Thread.Sleep(20);
			}

			Assert.True(Volatile.Read(ref chunks) > 0, "the recount did not start");
			gate.Set();
		});

	[Fact]
	public void TheSearchAndTheHitsOfTheFilterInput_AreCountedInTheBackground() =>
		WithBigList((viewer, entries) =>
		{
			var sevens = entries.Count(entry => entry.Message.EndsWith("seven"));

			viewer.FindText = "seven";
			Assert.True(WaitFor(() => viewer.FindResultText == $"{sevens} hits"));

			viewer.CurrentSearchText = "seven";
			Assert.True(WaitFor(() => viewer.FilterHitText == $"{sevens} hits"));
		});
}
