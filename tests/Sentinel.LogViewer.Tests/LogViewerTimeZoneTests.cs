using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using Sentinel.LogViewer.Wpf.Resolver;
using Sentinel.LogViewer.Wpf.XamlMultiValueConverter;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for the time zones of the viewer: the zone of the file and the display zone change what is shown, the header says so, and the cell
/// shows the original in its tooltip
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerTimeZoneTests
{
	private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");

	private readonly WpfStaContextFixture _context;

	public LogViewerTimeZoneTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static ObservableCollection<LogEntry> Entries() => new()
	{
		new LogEntry(LogEntryLevel.Info, "Acme.Orders", "from a file") { TimeStamp = new DateTime(2025, 12, 18, 14, 2, 11, 385) },
		new LogEntry(LogEntryLevel.Info, "Acme.Orders", "from the network") { TimeStamp = new DateTime(2025, 12, 18, 10, 0, 0, DateTimeKind.Utc) }
	};

	private static GridView ViewOf(Wpf.LogViewer viewer) => (GridView)((ListView)viewer.Template.FindName("PART_ListView", viewer)).View;

	private void WithViewer(Action<Wpf.LogViewer, ObservableCollection<LogEntry>> test) => _context.RunOnSta(() =>
	{
		var entries = Entries();
		var (window, viewer) = WpfTestHelper.ShowInWindow(entries);
		try
		{
			viewer.CustomTimeStampFormat = TimeStampFormat.Iso;
			test(viewer, entries);
		}
		finally
		{
			window.Close();
		}
	});

	[Fact]
	public void TheTimesAreShownInTheDisplayZone_ATimeWithoutAZoneIsReadInTheZoneOfTheFile() =>
		WithViewer((viewer, entries) =>
		{
			viewer.FileTimeZone = Tokyo;
			viewer.DisplayTimeZone = TimeZoneInfo.Utc;

			var resolver = (TimeStampResolver)viewer.TimeStampResolver;
			Assert.Equal("2025-12-18 05:02:11.385", resolver.Resolve(entries[0]));
			Assert.Equal("2025-12-18 10:00:00.000", resolver.Resolve(entries[1]));
			Assert.Equal(new DateTime(2025, 12, 18, 14, 2, 11, 385), entries[0].TimeStamp);
		});

	[Fact]
	public void TheHeaderSaysWhichZonesAreConverted() =>
		WithViewer((viewer, _) =>
		{
			var header = () => (string)ViewOf(viewer).Columns[2].Header;
			Assert.Equal("TimeStamp", header());

			viewer.FileTimeZone = Tokyo;
			Assert.Equal("TimeStamp (UTC+9 → local)", header());

			viewer.DisplayTimeZone = TimeZoneInfo.Utc;
			Assert.Equal("TimeStamp (UTC+9 → UTC)", header());

			viewer.FileTimeZone = null;
			Assert.Equal("TimeStamp (UTC)", header());

			viewer.DisplayTimeZone = null;
			Assert.Equal("TimeStamp", header());
		});

	[Fact]
	public void TheCellShowsTheOriginalInItsTooltip_OnlyWhileTheTimesAreConverted() =>
		WithViewer((viewer, entries) =>
		{
			var tooltip = new TimeStampOriginalConverter();
			object? Original() => tooltip.Convert(new object[] { entries[0], viewer.TimeStampResolver }, typeof(string), null!, null!);
			Assert.Null(Original());

			viewer.FileTimeZone = Tokyo;
			viewer.DisplayTimeZone = TimeZoneInfo.Utc;

			Assert.Equal("Original: 2025-12-18 14:02:11.385 (UTC+9)", Original());
			Assert.Equal("Original: 2025-12-18 10:00:00.000 (UTC)",
				tooltip.Convert(new object[] { entries[1], viewer.TimeStampResolver }, typeof(string), null!, null!));
		});

	[Fact]
	public void TheExportHasTheTimesAsTheyAreShown() =>
		WithViewer((viewer, entries) =>
		{
			viewer.FileTimeZone = Tokyo;
			viewer.DisplayTimeZone = TimeZoneInfo.Utc;

			var line = new DefaultLogExportFormatter().Format(entries[0], viewer.TimeStampResolver, new LoggerNameResolver(), new MessageResolver());

			Assert.StartsWith("2025-12-18 05:02:11.3850 |", line);
		});

	private sealed class NoCommand : ICommand
	{
		public event EventHandler? CanExecuteChanged { add { } remove { } }
		public bool CanExecute(object? parameter) => true;
		public void Execute(object? parameter) { }
	}

	[Fact]
	public void TheHeaderMenuOffersTheTimeZone_OnlyWhenTheApplicationGivesACommand() =>
		WithViewer((viewer, _) =>
		{
			var column = ViewOf(viewer).Columns[2];
			Assert.DoesNotContain(viewer.BuildColumnMenu(column, null).Items.OfType<MenuItem>(), item => item.Header as string == "Time zone...");

			viewer.ChangeTimeZoneCommand = new NoCommand();

			var menu = viewer.BuildColumnMenu(column, null);
			var entry = Assert.Single(menu.Items.OfType<MenuItem>(), item => item.Header as string == "Time zone...");
			Assert.Same(viewer.ChangeTimeZoneCommand, entry.Command);
		});
}
