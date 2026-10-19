using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf.Helper;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for <see cref="ColumnWidthFitter"/>
/// </summary>
[Collection(WpfCollection.Name)]
public class ColumnWidthFitterTests
{
	private const double InitialWidth = 40;
	private readonly WpfStaContextFixture _context;

	public ColumnWidthFitterTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static LogEntry Event(string message) => new(LogEntryLevel.Info, "Logger", message);

	private static double TextWidth(ListView listView, string text)
	{
		var typeface = new Typeface(listView.FontFamily, listView.FontStyle, listView.FontWeight, listView.FontStretch);
		return new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface,
			listView.FontSize, Brushes.Black, 1.0).WidthIncludingTrailingWhitespace;
	}

	[Fact]
	public void Include_WiderText_GrowsColumnToWidestLineOfAllItems()
	{
		_context.RunOnSta(() =>
		{
			var listView = new ListView();
			var column = new GridViewColumn { Width = InitialWidth };
			var fitter = new ColumnWidthFitter(listView);
			fitter.AddColumn(column, logEvent => logEvent.Message);
			const string longLine = "a considerably longer second line of the message";

			fitter.Include(new[] { Event("short"), Event("first line\n" + longLine) });
			fitter.Apply();

			var fittedWidth = column.Width;
			Assert.True(fittedWidth >= TextWidth(listView, longLine));
			Assert.True(fittedWidth < TextWidth(listView, "first line" + longLine));

			fitter.Include(new[] { Event("short again") });
			fitter.Apply();
			Assert.Equal(fittedWidth, column.Width);
		});
	}

	[Fact]
	public void Reset_WithoutItems_RestoresInitialColumnWidth()
	{
		_context.RunOnSta(() =>
		{
			var listView = new ListView();
			var column = new GridViewColumn { Width = InitialWidth };
			var fitter = new ColumnWidthFitter(listView);
			fitter.AddColumn(column, logEvent => logEvent.Message);
			fitter.Include(new[] { Event("a text that is wider than the initial column width") });
			fitter.Apply();

			fitter.Reset(Array.Empty<LogEntry>());
			Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, () => { });

			Assert.Equal(InitialWidth, column.Width);
		});
	}
}
