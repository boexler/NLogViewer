using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using WpfLogViewer = Sentinel.LogViewer.Wpf.LogViewer;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Helper class to run WPF tests on STA thread
/// </summary>
public static class WpfTestHelper
{
	/// <summary>
	/// Creates an LogViewer, feeds it with the given log events via a TestLogSource,
	/// and waits for the viewer to process them (async pipeline + dispatcher). Call from within RunOnStaThread.
	/// </summary>
	/// <param name="testData">Log events to feed into the viewer.</param>
	/// <returns>The viewer with the source set and events processed.</returns>
	public static WpfLogViewer CreateViewerWithTestData(IEnumerable<LogEntry> testData)
	{
		var list = testData?.ToList() ?? new List<LogEntry>();
		var viewer = new WpfLogViewer();
		var cache = new TestLogSource();
		cache.AddRange(list);
		viewer.StartListen(cache);
		WaitForViewerEvents(viewer, list.Count);
		return viewer;
	}

	/// <summary>
	/// Waits until the viewer's LogEvents.View contains at least expectedCount items (or timeout).
	/// Pumps the current dispatcher so that async subscription callbacks run.
	/// LogViewer uses SubscribeOn(Scheduler.Default) and Buffer(100ms), so an initial delay is needed.
	/// </summary>
	public static void WaitForViewerEvents(WpfLogViewer viewer, int expectedCount, int timeoutMs = 2000)
	{
		// Give the async pipeline time: subscription on thread pool + Buffer(100ms) + dispatch
		Thread.Sleep(200);
		var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
		while (DateTime.UtcNow < deadline)
		{
			Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, () => { });
			var count = viewer.LogEvents?.View?.Cast<LogEntry>().Count() ?? 0;
			if (count >= expectedCount) return;
			Thread.Sleep(50);
		}
	}

	/// <summary>
	/// Shows a <see cref="Wpf.LogViewer"/> for the given entries in an off-screen window, with the default theme applied.
	/// Call from within RunOnSta and close the window afterwards.
	/// </summary>
	public static (Window Window, Wpf.LogViewer Viewer) ShowInWindow(ObservableCollection<LogEntry> entries)
	{
		var viewer = new Wpf.LogViewer { ItemsSource = entries, AutoScroll = false };
		var window = new Window
		{
			Width = 900,
			Height = 500,
			Left = -10000,
			Top = -10000,
			ShowActivated = false,
			ShowInTaskbar = false,
			Content = viewer
		};
		window.Resources.MergedDictionaries.Add(new ResourceDictionary
		{
			Source = new Uri("pack://application:,,,/Sentinel.LogViewer.Wpf;component/Themes/Generic.xaml")
		});
		window.Show();
		Settle();
		return (window, viewer);
	}

	/// <summary>
	/// Lets the dispatcher finish layout, rendering and queued background work.
	/// </summary>
	public static void Settle()
	{
		for (var i = 0; i < 3; i++)
			Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, () => { });
	}
}
