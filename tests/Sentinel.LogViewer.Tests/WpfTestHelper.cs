using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
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
	/// <summary>Where <see cref="PrepareForTest{T}"/> puts a window; a window that is somewhere else was not prepared</summary>
	private const double PreparedLeft = -10000;

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
	/// Puts a window where nobody sees it and where it takes nothing from the user: far off the screen, not activated, without a button in the
	/// taskbar. Every test that shows a window prepares it with this before <c>Show()</c>;
	/// <see cref="Settle"/> fails when a window was shown without it. Limit: code of the application that calls <c>Activate()</c> itself
	/// (e.g. restoring from the tray) can still take the focus.
	/// </summary>
	/// <param name="invisible">For tests that minimize or maximize the window: it has no frame and is fully transparent, so it neither shows
	/// (a minimized window without a taskbar button would show its title bar on the desktop) nor catches clicks on the screen of the user</param>
	public static T PrepareForTest<T>(T window, bool invisible = false) where T : Window
	{
		window.WindowStartupLocation = WindowStartupLocation.Manual;
		window.Left = PreparedLeft;
		window.Top = -10000;
		window.ShowActivated = false;
		window.ShowInTaskbar = false;
		if (invisible)
		{
			window.WindowStyle = WindowStyle.None;
			window.AllowsTransparency = true;
			window.Opacity = 0;
		}

		return window;
	}

	private static readonly List<string> Unprepared = new();

	/// <summary>
	/// Notes a window that was loaded without <see cref="PrepareForTest{T}"/>; the fixture calls it for every window
	/// </summary>
	internal static void CheckPrepared(Window window)
	{
		if (window.Left != PreparedLeft)
			Unprepared.Add(window.GetType().Name);
	}

	/// <summary>
	/// Shows a <see cref="Wpf.LogViewer"/> for the given entries in an off-screen window, with the default theme applied.
	/// Call from within RunOnSta and close the window afterwards.
	/// </summary>
	public static (Window Window, Wpf.LogViewer Viewer) ShowInWindow(ObservableCollection<LogEntry> entries)
	{
		var viewer = new Wpf.LogViewer { ItemsSource = entries, AutoScroll = false };
		var window = PrepareForTest(new Window { Width = 900, Height = 500, Content = viewer });
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

		if (Unprepared.Count > 0)
		{
			var names = string.Join(", ", Unprepared);
			Unprepared.Clear();
			throw new InvalidOperationException($"A test showed a window without WpfTestHelper.PrepareForTest, so it showed on the screen: {names}");
		}
	}

	/// <summary>
	/// All the elements of a type below an element in the visual tree, depth first
	/// </summary>
	public static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
	{
		for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
		{
			var child = VisualTreeHelper.GetChild(root, i);
			if (child is T match)
				yield return match;

			foreach (var descendant in Descendants<T>(child))
				yield return descendant;
		}
	}
}
