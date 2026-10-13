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
}
