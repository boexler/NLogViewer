using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Parsers;
using Sentinel.LogViewer.Services;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// Tests for reading the timestamps of text files: dates and times in separate columns, AM / PM, day or month first, syslog and ctime
/// </summary>
[Collection("Culture")]
public class TimestampImportTests : IDisposable
{
	private readonly string _directory = Path.Combine(Path.GetTempPath(), "nlv_timestamps_" + Guid.NewGuid().ToString("N"));
	private readonly TextFileFormatDetector _detector = new();
	private readonly LogFileParserService _service;
	private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

	public TimestampImportTests()
	{
		Directory.CreateDirectory(_directory);
		_service = new LogFileParserService(new Log4JEventParser(), new PlainTextParser(), new JsonLogParser(),
			_detector, new TextFileFormatConfigService(Path.Combine(_directory, "formats.json")));
	}

	public void Dispose()
	{
		CultureInfo.CurrentCulture = _culture;
		_service.Dispose();
		Directory.Delete(_directory, recursive: true);
	}

	private string Write(IEnumerable<string> lines, DateTime? written = null)
	{
		var file = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".log");
		File.WriteAllLines(file, lines);
		if (written != null)
			File.SetLastWriteTime(file, written.Value);

		return file;
	}

	private TextFileFormat Detect(IEnumerable<string> lines)
	{
		var format = _detector.DetectFormat(Write(lines));
		Assert.NotNull(format);
		return format;
	}

	private async Task<List<LogEntry>> ImportAsync(IEnumerable<string> lines, DateTime? written = null)
	{
		var file = Write(lines, written);
		var format = _detector.DetectFormat(file);
		Assert.NotNull(format);
		return await _service.ParseFileAsync(file, textFormat: format);
	}

	private static IEnumerable<string> Lines(string timestampTemplate, string separator = " ") =>
		Enumerable.Range(0, 6).Select(i => string.Join(separator, string.Format(timestampTemplate, i), i % 2 == 0 ? "INFO" : "ERROR",
			"Acme.Shop.Orders.Service", "Message number " + i));

	[Fact]
	public void SpaceSeparatedFile_WithDateAndTimeInSeparateColumns_HasATimestampOfTwoColumns()
	{
		var format = Detect(Lines("2025-12-18 15:11:4{0}.385"));

		var mapping = format.ColumnMapping;
		Assert.Equal((0, 2), (mapping.TimestampColumn, mapping.TimestampColumnCount));
		Assert.Equal((2, 3, 4), (mapping.LevelColumn, mapping.LoggerColumn, mapping.MessageColumn));
	}

	[Fact]
	public async Task NLogDefaultLayout_KeepsTheDateTheLevelAndTheLogger()
	{
		var entries = await ImportAsync(Lines("2025-12-18 15:11:4{0}.385"));

		Assert.Equal(6, entries.Count);
		Assert.Equal(new DateTime(2025, 12, 18, 15, 11, 40, 385), entries[0].TimeStamp);
		Assert.Equal(new DateTime(2025, 12, 18, 15, 11, 45, 385), entries[5].TimeStamp);
		Assert.Equal(LogEntryLevel.Error, entries[1].Level);
		Assert.Equal("Acme.Shop.Orders.Service", entries[0].LoggerName);
		Assert.Equal("Message number 0", entries[0].Message);
	}

	[Fact]
	public void EnglishTimeWithAmAndPm_IsATimestampOfThreeColumns()
	{
		var format = Detect(Lines("12/18/2025 3:11:4{0} PM"));

		Assert.Equal((0, 3), (format.ColumnMapping.TimestampColumn, format.ColumnMapping.TimestampColumnCount));
		Assert.Equal(DateOrder.MonthFirst, format.DateOrder);
	}

	[Fact]
	public async Task EnglishTimeWithAmAndPm_IsReadWithTheRightDateAndHour()
	{
		var entries = await ImportAsync(Lines("12/18/2025 3:11:4{0} PM"));

		Assert.Equal(new DateTime(2025, 12, 18, 15, 11, 40), entries[0].TimeStamp);
		Assert.Equal("Acme.Shop.Orders.Service", entries[0].LoggerName);
	}

	[Fact]
	public async Task EnglishTimeWithAmAndPm_AlsoWorksWithAPipeAsSeparator()
	{
		var entries = await ImportAsync(Lines("12/18/2025 3:11:4{0} AM", " | "));

		Assert.Equal(new DateTime(2025, 12, 18, 3, 11, 40), entries[0].TimeStamp);
		Assert.Equal(LogEntryLevel.Info, entries[0].Level);
	}

	[Fact]
	public async Task DayFirst_IsDetectedFromADayAboveTwelve_AndRulesTheAmbiguousDates()
	{
		var lines = new[]
		{
			"01/02/2025 10:00:00 INFO Acme.Shop.Orders.Service first",
			"18/02/2025 10:00:00 INFO Acme.Shop.Orders.Service second",
			"03/02/2025 10:00:00 INFO Acme.Shop.Orders.Service third"
		};
		CultureInfo.CurrentCulture = new CultureInfo("en-US");

		var entries = await ImportAsync(lines);

		Assert.Equal(DateOrder.DayFirst, Detect(lines).DateOrder);
		Assert.Equal(new[] { new DateTime(2025, 2, 1, 10, 0, 0), new DateTime(2025, 2, 18, 10, 0, 0), new DateTime(2025, 2, 3, 10, 0, 0) },
			entries.Select(entry => entry.TimeStamp));
	}

	[Fact]
	public async Task MonthFirst_IsDetectedFromADayAboveTwelve_EvenOnAGermanComputer()
	{
		var lines = new[]
		{
			"01/02/2025 10:00:00 INFO Acme.Shop.Orders.Service first",
			"02/18/2025 10:00:00 INFO Acme.Shop.Orders.Service second",
			"03/02/2025 10:00:00 INFO Acme.Shop.Orders.Service third"
		};
		CultureInfo.CurrentCulture = new CultureInfo("de-DE");

		var entries = await ImportAsync(lines);

		Assert.Equal(DateOrder.MonthFirst, Detect(lines).DateOrder);
		Assert.Equal(new[] { new DateTime(2025, 1, 2, 10, 0, 0), new DateTime(2025, 2, 18, 10, 0, 0), new DateTime(2025, 3, 2, 10, 0, 0) },
			entries.Select(entry => entry.TimeStamp));
	}

	[Theory]
	[InlineData("de-DE", 2, 1)]
	[InlineData("en-US", 1, 2)]
	public async Task AFileThatDoesNotTellTheOrder_FollowsTheCultureOfTheComputer(string culture, int month, int day)
	{
		var lines = new[]
		{
			"01/02/2025 10:00:00 INFO Acme.Shop.Orders.Service first",
			"03/04/2025 10:00:00 INFO Acme.Shop.Orders.Service second",
			"05/06/2025 10:00:00 INFO Acme.Shop.Orders.Service third"
		};
		CultureInfo.CurrentCulture = new CultureInfo(culture);

		var entries = await ImportAsync(lines);

		Assert.Equal(DateOrder.Auto, Detect(lines).DateOrder);
		Assert.Equal(new DateTime(2025, month, day, 10, 0, 0), entries[0].TimeStamp);
	}

	[Fact]
	public async Task Syslog_TakesTheYearOfTheFile_AndTheYearBeforeForADateInTheFuture()
	{
		var lines = new[]
		{
			"Dec 18 15:11:48 INFO Acme.Shop.Orders.Service before the year",
			"Dec 31 23:59:59 INFO Acme.Shop.Orders.Service last second ever",
			"Jan  1 00:00:01 INFO Acme.Shop.Orders.Service first second ever"
		};

		var format = Detect(lines);
		var entries = await ImportAsync(lines, written: new DateTime(2026, 1, 2, 8, 0, 0));

		Assert.Equal((0, 3), (format.ColumnMapping.TimestampColumn, format.ColumnMapping.TimestampColumnCount));
		Assert.Equal(new[] { new DateTime(2025, 12, 18, 15, 11, 48), new DateTime(2025, 12, 31, 23, 59, 59), new DateTime(2026, 1, 1, 0, 0, 1) },
			entries.Select(entry => entry.TimeStamp));
		Assert.Equal("Acme.Shop.Orders.Service", entries[0].LoggerName);
	}

	[Fact]
	public async Task Ctime_IsATimestampOfFiveColumns()
	{
		var lines = Lines("Thu Dec 18 15:11:4{0} 2025");

		var format = Detect(lines);
		var entries = await ImportAsync(lines);

		Assert.Equal((0, 5), (format.ColumnMapping.TimestampColumn, format.ColumnMapping.TimestampColumnCount));
		Assert.Equal(new DateTime(2025, 12, 18, 15, 11, 40), entries[0].TimeStamp);
	}

	[Fact]
	public async Task MonthNames_AreReadWithTheYearBeforeTheTime()
	{
		var entries = await ImportAsync(Lines("18 Dec 2025 15:11:4{0}"));

		Assert.Equal(new DateTime(2025, 12, 18, 15, 11, 40), entries[0].TimeStamp);
		Assert.Equal(LogEntryLevel.Info, entries[0].Level);
	}

	[Fact]
	public async Task StackTraceLines_AreStillPartOfTheMessage_WithDatesThatAreNotIso()
	{
		var entries = await ImportAsync(new[]
		{
			"12/18/2025 3:11:40 PM ERROR Acme.Shop.Orders.Service the order failed",
			"   at Acme.Shop.Orders.Process() in Orders.cs:line 12",
			"   at Acme.Shop.Program.Main() in Program.cs:line 5",
			"12/18/2025 3:11:41 PM INFO Acme.Shop.Orders.Service next order",
			"12/18/2025 3:11:42 PM INFO Acme.Shop.Orders.Service another order"
		});

		Assert.Equal(3, entries.Count);
		Assert.Contains("Orders.cs:line 12", entries[0].Message);
		Assert.Contains("Program.cs:line 5", entries[0].Message);
		Assert.Equal(new DateTime(2025, 12, 18, 15, 11, 41), entries[1].TimeStamp);
	}

	[Fact]
	public async Task IsoFilesWithAPipe_AreReadAsBefore()
	{
		var lines = new[]
		{
			"2025-12-18 15:11:48.385 | INFO | Acme.Shop.Orders.Service | first",
			"2025-12-18 15:11:49.385 | ERROR | Acme.Shop.Orders.Service | second",
			"2025-12-18 15:11:50.385 | INFO | Acme.Shop.Orders.Service | third"
		};

		var format = Detect(lines);
		var entries = await ImportAsync(lines);

		Assert.Equal((0, 1), (format.ColumnMapping.TimestampColumn, format.ColumnMapping.TimestampColumnCount));
		Assert.Equal(new DateTime(2025, 12, 18, 15, 11, 48, 385), entries[0].TimeStamp);
		Assert.Equal(DateOrder.Auto, format.DateOrder);
	}

	[Fact]
	public void TheMappedColumns_IncludeAllColumnsOfTheTimestamp()
	{
		var mapping = new ColumnMapping { TimestampColumn = 1, TimestampColumnCount = 3, LevelColumn = 4, MessageColumn = 6 };

		Assert.Equal(new[] { 1, 2, 3, 4, 6 }, mapping.GetMappedColumns());
	}

	[Fact]
	public void TheFormat_KeepsItsNewValues_WhenSavedAndLoaded()
	{
		var format = new TextFileFormat
		{
			Separator = TextLineSplitter.SpacesSeparator,
			DateOrder = DateOrder.DayFirst,
			ColumnMapping = new ColumnMapping { TimestampColumn = 0, TimestampColumnCount = 2, MessageColumn = 3 }
		};

		var json = System.Text.Json.JsonSerializer.Serialize(format);
		var loaded = System.Text.Json.JsonSerializer.Deserialize<TextFileFormat>(json)!;

		Assert.Equal(DateOrder.DayFirst, loaded.DateOrder);
		Assert.Equal(2, loaded.ColumnMapping.TimestampColumnCount);

		var old = System.Text.Json.JsonSerializer.Deserialize<TextFileFormat>("{\"Separator\":\"|\",\"ColumnMapping\":{\"TimestampColumn\":0,\"MessageColumn\":1}}")!;
		Assert.Equal(1, old.ColumnMapping.TimestampColumnCount);
		Assert.Equal(DateOrder.Auto, old.DateOrder);
	}

	/// <summary>
	/// Messages with a very different number of words: the columns do not line up, so the file has no format and its lines are read one by one
	/// </summary>
	private static readonly string[] Messages =
	{
		"started", "this is a much longer message with many words in it", "two words", "and now a really quite long message with even more words than before",
		"three small words", "a message of about eight words in total"
	};

	private static IEnumerable<string> LinesWithDifferentMessages(string timestampTemplate) =>
		Messages.Select((message, i) => $"{string.Format(timestampTemplate, i)} {(i % 2 == 0 ? "INFO" : "ERROR")} {message}");

	[Theory]
	[InlineData("12/18/2025 3:11:4{0} PM", 2025, 12, 18, 15)]
	[InlineData("18/12/2025 15:11:4{0}", 2025, 12, 18, 15)]
	[InlineData("18 Dec 2025 15:11:4{0}", 2025, 12, 18, 15)]
	[InlineData("Thu Dec 18 15:11:4{0} 2025", 2025, 12, 18, 15)]
	[InlineData("2025-12-18 15:11:4{0}.385", 2025, 12, 18, 15)]
	public async Task LinesReadOneByOne_AlsoKnowTheDateOfTheCommonFormats(string timestampTemplate, int year, int month, int day, int hour)
	{
		var lines = LinesWithDifferentMessages(timestampTemplate).ToList();
		var file = Write(lines);
		Assert.Null(_detector.DetectFormat(file));

		var entries = await _service.ParseFileAsync(file, textFormat: null);

		Assert.Equal(6, entries.Count);
		Assert.Equal(new DateTime(year, month, day, hour, 11, 40).Date, entries[0].TimeStamp.Date);
		Assert.Equal(new[] { 40, 41, 42, 43, 44, 45 }, entries.Select(entry => entry.TimeStamp.Second));
		Assert.All(entries, entry => Assert.Equal(hour, entry.TimeStamp.Hour));
		Assert.Equal(LogEntryLevel.Error, entries[1].Level);
	}

	[Fact]
	public async Task LinesReadOneByOne_TakeTheYearOfSyslogFromTheFile()
	{
		var lines = LinesWithDifferentMessages("Dec 18 15:11:4{0}").ToList();
		var file = Write(lines, written: new DateTime(2026, 1, 2, 8, 0, 0));
		Assert.Null(_detector.DetectFormat(file));

		var entries = await _service.ParseFileAsync(file, textFormat: null);

		Assert.All(entries, entry => Assert.Equal(new DateTime(2025, 12, 18), entry.TimeStamp.Date));
	}
}
