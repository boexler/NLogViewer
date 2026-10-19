using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using NLog;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf.Targets;
using Xunit;
using ViewerControl = Sentinel.LogViewer.Wpf.LogViewer;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for the entries of the viewer: its source, the logging of NLog as the default, and listening again when the control is loaded again
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerSourceTests
{
	private readonly WpfStaContextFixture _context;

	public LogViewerSourceTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static Window Show(ViewerControl viewer)
	{
		var window = new Window { Width = 600, Height = 400, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false, Content = viewer };
		window.Resources.MergedDictionaries.Add(new ResourceDictionary
		{
			Source = new Uri("pack://application:,,,/Sentinel.LogViewer.Wpf;component/Themes/Generic.xaml")
		});
		window.Show();
		WpfTestHelper.Settle();
		return window;
	}

	private static string[] Messages(ViewerControl viewer) =>
		viewer.LogEvents.View.Cast<LogEntry>().Select(entry => entry.Message).ToArray();

	/// <summary>
	/// Lets the buffer of the subscription (100 ms) and the dispatcher run
	/// </summary>
	private static void Wait()
	{
		for (var i = 0; i < 8; i++)
		{
			Thread.Sleep(60);
			WpfTestHelper.Settle();
		}
	}

	private static LogEntry Entry(string message) => new(LogEntryLevel.Info, "Logger", message);

	[Fact]
	public void TheViewerShowsTheEntriesOfItsSource_AlsoTheOnesThatComeLater()
	{
		_context.RunOnSta(() =>
		{
			var source = new TestLogSource();
			source.Add(Entry("early"));
			var viewer = new ViewerControl { Source = source };
			var window = Show(viewer);
			try
			{
				Wait();
				Assert.Equal(new[] { "early" }, Messages(viewer));

				source.Add(Entry("late"));
				Wait();
				Assert.Equal(new[] { "early", "late" }, Messages(viewer));
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void AViewerThatIsLoadedAgain_ListensAgain_WithoutShowingEntriesTwice()
	{
		_context.RunOnSta(() =>
		{
			var source = new TestLogSource();
			source.Add(Entry("one"));
			var viewer = new ViewerControl { Source = source };
			var window = Show(viewer);
			try
			{
				Wait();

				// A tab or a docking panel takes the control out of the window and puts it back.
				window.Content = null;
				Wait();
				source.Add(Entry("while away"));
				window.Content = viewer;
				Wait();
				Assert.Equal(new[] { "one", "while away" }, Messages(viewer));

				source.Add(Entry("after coming back"));
				Wait();
				Assert.Equal(new[] { "one", "while away", "after coming back" }, Messages(viewer));
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void APausedViewer_StaysPausedWhenItIsLoadedAgain_AndListensAgainWhenTheBreakEnds()
	{
		_context.RunOnSta(() =>
		{
			var source = new TestLogSource();
			var viewer = new ViewerControl { Source = source, Pause = true };
			var window = Show(viewer);
			try
			{
				source.Add(Entry("while paused"));
				Wait();
				Assert.Empty(Messages(viewer));

				window.Content = null;
				Wait();
				window.Content = viewer;
				Wait();
				Assert.Empty(Messages(viewer));

				viewer.Pause = false;
				Wait();
				Assert.Equal(new[] { "while paused" }, Messages(viewer));
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void TheItemsSource_WinsOverTheSource()
	{
		_context.RunOnSta(() =>
		{
			var source = new TestLogSource();
			source.Add(Entry("from the source"));
			var items = new ObservableCollection<LogEntry> { Entry("from the caller") };
			var viewer = new ViewerControl { Source = source, ItemsSource = items };
			var window = Show(viewer);
			try
			{
				Wait();
				Assert.Equal(new[] { "from the caller" }, Messages(viewer));
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void ChangingTheSource_ShowsTheEntriesOfTheNewOne()
	{
		_context.RunOnSta(() =>
		{
			var first = new TestLogSource();
			first.Add(Entry("first source"));
			var second = new TestLogSource();
			second.Add(Entry("second source"));
			var viewer = new ViewerControl { Source = first };
			var window = Show(viewer);
			try
			{
				Wait();
				Assert.Equal(new[] { "first source" }, Messages(viewer));

				viewer.Source = second;
				Wait();
				Assert.Equal(new[] { "second source" }, Messages(viewer));
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void WithoutASource_TheViewerShowsTheLoggingOfNLog()
	{
		_context.RunOnSta(() =>
		{
			var targetName = "viewer-source-test-" + Guid.NewGuid().ToString("N");
			var viewer = new ViewerControl { TargetName = targetName };
			var window = Show(viewer);
			try
			{
				Wait();
				LogManager.GetLogger("Acme.SourceTest").Info("written with NLog");
				Wait();

				Assert.Contains("written with NLog", Messages(viewer));
				Assert.IsType<CacheTarget>(CacheTarget.GetInstance(targetName: targetName));
			}
			finally
			{
				window.Close();
			}
		});
	}
}
