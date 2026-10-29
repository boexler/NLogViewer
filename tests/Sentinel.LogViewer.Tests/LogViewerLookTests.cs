using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using Xunit;
using static Sentinel.LogViewer.Tests.WpfTestHelper;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for the look of the rows: how the level is shown and how high the rows are
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerLookTests
{
	private readonly WpfStaContextFixture _context;

	public LogViewerLookTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static ObservableCollection<LogEntry> Entries() => new()
	{
		new LogEntry(LogEntryLevel.Info, "Acme.Orders", "created"),
		new LogEntry(LogEntryLevel.Error, "Acme.Billing", "failed")
	};

	private void WithViewer(System.Action<Wpf.LogViewer> test) => _context.RunOnSta(() =>
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

	private static ListViewItem FirstRow(Wpf.LogViewer viewer) => Descendants<ListViewItem>(viewer).First();

	private static (Image Symbol, TextBlock Text) LevelCell(Wpf.LogViewer viewer)
	{
		var row = FirstRow(viewer);
		return (Descendants<Image>(row).Single(), Descendants<TextBlock>(row).Single(text => text.Text == "Info"));
	}

	[Fact]
	public void TheLevel_IsShownAsSymbol_AsText_OrAsBoth()
	{
		WithViewer(viewer =>
		{
			var (symbol, text) = LevelCell(viewer);
			Assert.Equal(Visibility.Visible, symbol.Visibility);
			Assert.Equal(Visibility.Collapsed, text.Visibility);

			viewer.LevelDisplay = LevelDisplayMode.Text;
			WpfTestHelper.Settle();
			Assert.Equal(Visibility.Collapsed, symbol.Visibility);
			Assert.Equal(Visibility.Visible, text.Visibility);

			viewer.LevelDisplay = LevelDisplayMode.SymbolAndText;
			WpfTestHelper.Settle();
			Assert.Equal(Visibility.Visible, symbol.Visibility);
			Assert.Equal(Visibility.Visible, text.Visibility);
		});
	}

	[Fact]
	public void TheRowsAndTheirCells_ReadTheLookFromThemselves_NotFromASearchForTheViewer()
	{
		WithViewer(viewer =>
		{
			viewer.LevelDisplay = LevelDisplayMode.Text;
			viewer.RowHeight = RowHeightMode.Compact;
			WpfTestHelper.Settle();

			var row = FirstRow(viewer);
			Assert.Equal(RowHeightMode.Compact, Wpf.LogViewer.GetRowHeight(row));
			Assert.Equal(LevelDisplayMode.Text, Wpf.LogViewer.GetLevelDisplay(Descendants<Image>(row).Single()));
		});
	}

	[Fact]
	public void TheRowsAndTheirCells_KnowTheViewer_ForTheirColoursAndResolvers()
	{
		WithViewer(viewer =>
		{
			var row = FirstRow(viewer);
			viewer.InfoBackground = Brushes.Orchid;
			WpfTestHelper.Settle();

			Assert.Same(viewer, Wpf.LogViewer.GetOwner(row));
			Assert.Same(viewer, Wpf.LogViewer.GetOwner(Descendants<Image>(row).Single()));
			Assert.Same(Brushes.Orchid, row.Background);
			Assert.Contains(Descendants<TextBlock>(row), text => text.Text == "created");
			Assert.Contains(Descendants<TextBlock>(row), text => text.Text == "Acme.Orders");
			Assert.All(Descendants<TextBlock>(row).Where(text => text.Tag != null), text => Assert.Same(viewer, text.Tag));
			Assert.DoesNotContain(Descendants<TextBlock>(row), text => text.Text == "####");
		});
	}

	[Fact]
	public void TheLevelColumn_IsAsWideAsWhatItShows()
	{
		WithViewer(viewer =>
		{
			var column = ((GridView)((ListView)viewer.Template.FindName("PART_ListView", viewer)).View).Columns[2];
			var symbolWidth = column.ActualWidth;

			viewer.LevelDisplay = LevelDisplayMode.SymbolAndText;
			WpfTestHelper.Settle();

			Assert.True(column.ActualWidth > symbolWidth + 10, $"{column.ActualWidth} should be wider than {symbolWidth}");
		});
	}

	[Fact]
	public void CompactRows_AreLowerThanNormalOnes()
	{
		WithViewer(viewer =>
		{
			var normal = FirstRow(viewer).ActualHeight;

			viewer.RowHeight = RowHeightMode.Compact;
			WpfTestHelper.Settle();
			var compact = FirstRow(viewer).ActualHeight;
			Assert.True(compact < normal, $"{compact} should be lower than {normal}");

			viewer.RowHeight = RowHeightMode.Normal;
			WpfTestHelper.Settle();
			Assert.Equal(normal, FirstRow(viewer).ActualHeight);
		});
	}
}
