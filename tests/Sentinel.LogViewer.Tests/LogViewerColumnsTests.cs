using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for the columns of the list: which are shown, in which order, the thread and the properties
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerColumnsTests
{
	private readonly WpfStaContextFixture _context;

	public LogViewerColumnsTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static ObservableCollection<LogEntry> Entries() => new()
	{
		new LogEntry(LogEntryLevel.Info, "Acme.Orders", "created")
		{
			Properties = new Dictionary<string, object?> { ["Thread"] = "T-1", ["Class"] = "OrderService", ["Method"] = "Create" }
		},
		new LogEntry(LogEntryLevel.Error, "Acme.Billing", "failed") { Properties = new Dictionary<string, object?> { ["Class"] = "Billing", ["Amount"] = 12.5 } }
	};

	private static GridView ViewOf(Wpf.LogViewer viewer) => (GridView)((ListView)viewer.Template.FindName("PART_ListView", viewer)).View;

	private static string[] Headers(Wpf.LogViewer viewer) => ViewOf(viewer).Columns.Select(column => (string)column.Header).ToArray();

	private void WithViewer(Action<Wpf.LogViewer> test) => _context.RunOnSta(() =>
	{
		var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
		try
		{
			test(viewer);
		}
		finally
		{
			window.Close();
		}
	});

	[Fact]
	public void TheList_HasTheFiveColumnsOfBefore_ByDefault() =>
		WithViewer(viewer =>
		{
			Assert.Equal(new[] { "ID", "Level", "TimeStamp", "LoggerName", "Message" }, Headers(viewer));
			Assert.Equal(LogColumns.Default, viewer.ColumnKeys);
		});

	[Fact]
	public void TheThreadColumn_IsOffByDefault_AndShowsTheThreadWhenItIsOn() =>
		WithViewer(viewer =>
		{
			viewer.SetColumnVisible(LogColumns.Thread, true);

			Assert.Equal(new[] { "ID", "Level", "TimeStamp", "LoggerName", "Thread", "Message" }, Headers(viewer));
			var column = ViewOf(viewer).Columns[4];
			var binding = (Binding)column.DisplayMemberBinding;
			var converter = (IValueConverter)binding.Converter;
			var entries = Entries();
			Assert.Equal("T-1", converter.Convert(entries[0], typeof(string), binding.ConverterParameter, CultureInfo.InvariantCulture));
			Assert.Null(converter.Convert(entries[1], typeof(string), binding.ConverterParameter, CultureInfo.InvariantCulture));
		});

	[Fact]
	public void ThePropertiesOfTheEntries_CanBeShownAsColumns_BeforeTheMessage() =>
		WithViewer(viewer =>
		{
			viewer.SetColumnVisible(LogColumns.Property("Class"), true);
			viewer.SetColumnVisible(LogColumns.Property("Amount"), true);

			Assert.Equal(new[] { "ID", "Level", "TimeStamp", "LoggerName", "Class", "Amount", "Message" }, Headers(viewer));

			viewer.SetColumnVisible(LogColumns.Property("Class"), false);
			Assert.Equal(new[] { "ID", "Level", "TimeStamp", "LoggerName", "Amount", "Message" }, Headers(viewer));
		});

	[Fact]
	public void TheOrderOfTheKeys_IsTheOrderOfTheColumns_AndTheMessageIsAlwaysThere() =>
		WithViewer(viewer =>
		{
			viewer.ColumnKeys = new[] { LogColumns.Level, LogColumns.Id, LogColumns.Message, LogColumns.TimeStamp, "unknown", LogColumns.Level };

			Assert.Equal(new[] { "Level", "ID", "Message", "TimeStamp" }, Headers(viewer));

			viewer.ColumnKeys = new[] { LogColumns.Logger };
			Assert.Equal(new[] { "LoggerName", "Message" }, Headers(viewer));
		});

	[Fact]
	public void TheShowColumnProperties_AndTheLayout_FollowEachOther() =>
		WithViewer(viewer =>
		{
			viewer.ShowLevelColumn = false;
			Assert.DoesNotContain(LogColumns.Level, viewer.ColumnKeys);
			Assert.Equal(new[] { "ID", "TimeStamp", "LoggerName", "Message" }, Headers(viewer));

			viewer.ShowLevelColumn = true;
			Assert.Equal(LogColumns.Default, viewer.ColumnKeys);

			viewer.ColumnKeys = new[] { LogColumns.Message };
			Assert.False(viewer.ShowIdColumn);
			Assert.False(viewer.ShowLoggerNameColumn);
			viewer.ShowIdColumn = true;
			Assert.Equal(new[] { "ID", "Message" }, Headers(viewer));
		});

	[Fact]
	public void MovingAColumnHeader_ChangesTheLayout() =>
		WithViewer(viewer =>
		{
			ViewOf(viewer).Columns.Move(0, 2);

			Assert.Equal(new[] { LogColumns.Level, LogColumns.TimeStamp, LogColumns.Id, LogColumns.Logger, LogColumns.Message }, viewer.ColumnKeys);
			Assert.Equal(new[] { "Level", "TimeStamp", "ID", "LoggerName", "Message" }, Headers(viewer));
		});

	[Fact]
	public void ThePropertyNames_AreCollected_WithoutTheThread_AndAlsoFromEntriesThatArriveLater() =>
		WithViewer(viewer =>
		{
			Assert.Equal(new[] { "Amount", "Class", "Method" }, viewer.KnownPropertyNames.OrderBy(name => name));

			var entries = (ObservableCollection<LogEntry>)viewer.ItemsSource!;
			entries.Add(new LogEntry(LogEntryLevel.Info, "L", "m") { Properties = new Dictionary<string, object?> { ["Zone"] = 1 } });

			Assert.Equal(new[] { "Amount", "Class", "Method", "Zone" }, viewer.KnownPropertyNames.OrderBy(name => name));
		});

	[Fact]
	public void TheMenuOfTheHeaders_ListsTheColumnsWithTheirState_AndSwitchesThem() =>
		WithViewer(viewer =>
		{
			var menu = viewer.BuildColumnMenu(ViewOf(viewer).Columns[0], null);

			var columns = menu.Items.OfType<MenuItem>().Where(item => item.IsCheckable).ToList();
			Assert.Equal(new[] { "ID", "Level", "TimeStamp", "LoggerName", "Thread", "Message" }, columns.Select(item => (string)item.Header));
			Assert.Equal(new[] { true, true, true, true, false, true }, columns.Select(item => item.IsChecked));
			Assert.False(columns[5].IsEnabled);
			var properties = menu.Items.OfType<MenuItem>().Single(item => item.Header as string == "Properties");
			Assert.Equal(new[] { "Amount", "Class", "Method" }, properties.Items.OfType<MenuItem>().Select(item => (string)item.Header));

			var thread = columns[4];
			thread.IsChecked = true;
			thread.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));
			Assert.Contains(LogColumns.Thread, viewer.ColumnKeys);

			var method = properties.Items.OfType<MenuItem>().Last();
			method.IsChecked = true;
			method.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));
			Assert.Contains(LogColumns.Property("Method"), viewer.ColumnKeys);
		});

	[Fact]
	public void TheValueOfAProperty_IsFoundWhateverTheCaseOfItsName()
	{
		var entry = new LogEntry(LogEntryLevel.Info, "L", "m") { Properties = new Dictionary<string, object?> { ["class"] = "Billing" } };

		Assert.Equal("Billing", LogColumns.ValueOf(entry, "Class"));
		Assert.Equal("Billing", LogColumns.ValueOf(entry, "class"));
		Assert.Null(LogColumns.ValueOf(entry, "Method"));
	}

	[Theory]
	[InlineData("thread", true, "id,level,timestamp,logger,thread,message")]
	[InlineData("property:Class", true, "id,level,timestamp,logger,property:Class,message")]
	[InlineData("level", false, "id,timestamp,logger,message")]
	[InlineData("message", false, "id,level,timestamp,logger,message")]
	[InlineData("id", true, "id,level,timestamp,logger,message")]
	public void ShowingAndHidingAColumn_KeepsItsPlace(string key, bool visible, string expected) =>
		Assert.Equal(expected, string.Join(",", LogColumns.With(LogColumns.Default, key, visible)));

	[Fact]
	public void AColumnThatIsShownAgain_GoesBackAfterTheColumnsThatComeBeforeIt()
	{
		var layout = new[] { "logger", "property:Class", "message" };

		Assert.Equal("id,logger,property:Class,message", string.Join(",", LogColumns.With(layout, "id", true)));
		Assert.Equal("logger,thread,property:Class,message", string.Join(",", LogColumns.With(layout, "thread", true)));
		Assert.Equal("logger,property:Class,message", string.Join(",", LogColumns.With(layout, "message", true)));
	}
}
