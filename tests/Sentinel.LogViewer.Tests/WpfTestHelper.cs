using System.Windows.Threading;
using NLog;
using WpfNLogViewer = Sentinel.NLogViewer.Wpf.NLogViewer;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Helper class to run WPF tests on STA thread
/// </summary>
public static class WpfTestHelper
{
	/// <summary>
	/// Creates an NLogViewer, feeds it with the given log events via a TestCacheTarget,
	/// and waits for the viewer to process them (async pipeline + dispatcher). Call from within RunOnStaThread.
	/// </summary>
	/// <param name="testData">Log events to feed into the viewer.</param>
	/// <returns>The viewer with CacheTarget set and events processed.</returns>
	public static WpfNLogViewer CreateViewerWithTestData(IEnumerable<LogEventInfo> testData)
	{
		var list = testData?.ToList() ?? new List<LogEventInfo>();
		var viewer = new WpfNLogViewer();
		var cache = new TestCacheTarget();
		cache.AddRange(list);
		viewer.StartListen(cache);
		WaitForViewerEvents(viewer, list.Count);
		return viewer;
	}

	/// <summary>
	/// Waits until the viewer's LogEvents.View contains at least expectedCount items (or timeout).
	/// Pumps the current dispatcher so that async subscription callbacks run.
	/// NLogViewer uses SubscribeOn(Scheduler.Default) and Buffer(100ms), so an initial delay is needed.
	/// </summary>
	public static void WaitForViewerEvents(WpfNLogViewer viewer, int expectedCount, int timeoutMs = 2000)
	{
		// Give the async pipeline time: subscription on thread pool + Buffer(100ms) + dispatch
		Thread.Sleep(200);
		var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
		while (DateTime.UtcNow < deadline)
		{
			Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, () => { });
			var count = viewer.LogEvents?.View?.Cast<LogEventInfo>().Count() ?? 0;
			if (count >= expectedCount) return;
			Thread.Sleep(50);
		}
	}
}
