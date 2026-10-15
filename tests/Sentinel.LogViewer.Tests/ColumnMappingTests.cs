using System.Collections.Generic;
using System.Linq;
using NLog;
using Sentinel.LogViewer.Helpers;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Parsers;
using Sentinel.LogViewer.ViewModels;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for correcting a detected text format in the column mapping dialog and for the ID shown per entry.
/// </summary>
public class ColumnMappingTests
{
	[Fact]
	public void Separator_Changed_ResplitsColumnsAndIsUsedByFinalFormat()
	{
		var wrongFormat = new TextFileFormat { Separator = "\t", ColumnCount = 1 };
		var sampleLines = new List<string> { "T+0000001;INFO;My.Logger.Name;\"quoted;message\"" };
		var viewModel = new ColumnMappingViewModel(wrongFormat, sampleLines, "sample.log");
		Assert.Single(viewModel.Columns);

		viewModel.Separator = ";";
		viewModel.Columns[1].MappedField = "Level";
		viewModel.Columns[3].MappedField = "Message";

		var format = viewModel.FinalFormat;
		Assert.Equal(new[] { "T+0000001", "INFO", "My.Logger.Name", "\"quoted" }, viewModel.Columns.Select(c => c.SampleData).Take(4));
		Assert.Equal(";", format.Separator);
		Assert.True(format.QuotedFields);
		Assert.Equal(1, format.ColumnMapping.LevelColumn);
		Assert.Equal(3, format.ColumnMapping.MessageColumn);
	}

	[Fact]
	public void Parse_MessageMappedBeforeUnmappedColumns_TakesRestOfLine()
	{
		// Every message contains the separator, so five columns are detected instead of four.
		var format = new TextFileFormat
		{
			Separator = " | ",
			ColumnCount = 5,
			ColumnMapping = new ColumnMapping { TimestampColumn = 0, LevelColumn = 1, LoggerColumn = 2, MessageColumn = 3 }
		};

		var entries = new PlainTextParser().Parse(
			new[] { "2025-01-01 10:00:00 | INFO | My.Logger | Verbindung | Timeout nach 30s" }, format);

		Assert.Equal("Verbindung | Timeout nach 30s", Assert.Single(entries).Message);
	}

	[Fact]
	public void FileLineIdResolver_ImportedEntry_ReturnsFileLine()
	{
		var logEvent = new LogEventInfo(LogLevel.Info, "Logger", "Message");
		logEvent.Properties[PlainTextParser.FileLinePropertyName] = 74;

		Assert.Equal("74", new FileLineIdResolver().Resolve(logEvent));
	}

	private static ColumnMappingViewModel SpaceSeparatedDialog(int timestampColumnCount = 2, DateOrder dateOrder = DateOrder.Auto)
	{
		var detected = new TextFileFormat
		{
			Separator = TextLineSplitter.SpacesSeparator,
			ColumnCount = 6,
			DateOrder = dateOrder,
			ColumnMapping = new ColumnMapping { TimestampColumn = 0, TimestampColumnCount = timestampColumnCount, LevelColumn = 2, LoggerColumn = 3, MessageColumn = 4 }
		};
		var sampleLines = new List<string> { "2025-12-18 15:11:48.385 INFO Acme.Shop.Orders.Service hello world" };
		return new ColumnMappingViewModel(detected, sampleLines, "sample.log");
	}

	[Fact]
	public void TimestampOfSeveralColumns_IsShownOnAllItsColumns_AndKeptInTheFinalFormat()
	{
		var viewModel = SpaceSeparatedDialog();

		Assert.Equal(new[] { "Timestamp", "Timestamp", "Level", "Logger", "Message", "None" }, viewModel.Columns.Select(c => c.MappedField));
		var mapping = viewModel.FinalFormat.ColumnMapping;
		Assert.Equal((0, 2), (mapping.TimestampColumn, mapping.TimestampColumnCount));
		Assert.Contains("Timestamp: 2025-12-18 15:11:48.385 |", viewModel.PreviewResults[0].SampleData);
	}

	[Fact]
	public void MappingAnotherColumnToTheTimestamp_ExtendsItOnlyWhenTheColumnsAreNextToEachOther()
	{
		var viewModel = SpaceSeparatedDialog(timestampColumnCount: 1);

		viewModel.Columns[1].MappedField = "Timestamp";
		Assert.Equal(2, viewModel.FinalFormat.ColumnMapping.TimestampColumnCount);

		viewModel.Columns[5].MappedField = "Timestamp";
		Assert.Equal(2, viewModel.FinalFormat.ColumnMapping.TimestampColumnCount);

		viewModel.Columns[0].MappedField = "None";
		viewModel.Columns[1].MappedField = "None";
		Assert.Equal((5, 1), (viewModel.FinalFormat.ColumnMapping.TimestampColumn, viewModel.FinalFormat.ColumnMapping.TimestampColumnCount));
	}

	[Fact]
	public void DateOrder_ComesFromTheDetectedFormat_CanBeChanged_AndIsUsedByTheFinalFormat()
	{
		var viewModel = SpaceSeparatedDialog(dateOrder: DateOrder.MonthFirst);
		Assert.Equal(DateOrder.MonthFirst, viewModel.DateOrder);
		Assert.Equal(DateOrder.MonthFirst, viewModel.FinalFormat.DateOrder);

		viewModel.DateOrder = DateOrder.DayFirst;

		Assert.Equal(DateOrder.DayFirst, viewModel.FinalFormat.DateOrder);
		Assert.Equal(new[] { DateOrder.Auto, DateOrder.DayFirst, DateOrder.MonthFirst }, viewModel.DateOrders.Values);
		Assert.StartsWith("Automatic (", viewModel.DateOrders.Keys.First());
	}
}
