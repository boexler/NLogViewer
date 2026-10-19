using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Parsers;
using Sentinel.LogViewer.Services;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// Tests for importing text log files through <see cref="LogFileParserService"/> with the detected format.
/// </summary>
public class LogFileParserServiceTests : IDisposable
{
	private readonly string _directory = Path.Combine(Path.GetTempPath(), "nlv_parse_" + Guid.NewGuid().ToString("N"));
	private readonly TextFileFormatDetector _detector = new();
	private readonly LogFileParserService _service;

	public LogFileParserServiceTests()
	{
		Directory.CreateDirectory(_directory);
		_service = new LogFileParserService(new Log4JEventParser(), new PlainTextParser(), new JsonLogParser(),
			_detector, new TextFileFormatConfigService());
	}

	public void Dispose()
	{
		_service.Dispose();
		Directory.Delete(_directory, recursive: true);
	}

	private async Task<List<LogEntry>> ParseAsync(IEnumerable<string> lines)
	{
		var file = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".log");
		File.WriteAllLines(file, lines);

		// Passing the detected format keeps formats saved on this machine out of the test.
		var format = _detector.DetectFormat(file);
		Assert.NotNull(format);
		return await _service.ParseFileAsync(file, textFormat: format);
	}

	private static string PipeLine(int number, string message = "Message number") =>
		$"2025-01-01 10:00:00.000 | INFO | My.Logger.Name | {message} {number}";

	private static object? FileLine(LogEntry logEvent) => logEvent.Properties[PlainTextParser.FileLinePropertyName];

	[Fact]
	public async Task ParseFileAsync_QuotedTabSeparatedFile_ParsesEntriesWithMultiLineMessages()
	{
		var entries = await ParseAsync(new[]
		{
			"2026-01-01 07:54:09.928\tDEBUG\tMy.App.OpcService\tadd new channel for endpoint host:4840",
			"2026-01-01 07:54:09.928\tDEBUG\tMy.App.OpcService\t\"add new handler \"\"Watchdog_V1\"\"\"",
			"2026-01-01 07:54:10.110\tDEBUG\tMy.App.OptionsSetupService\t\"{",
			"  \"\"Enabled\"\": true,",
			"  \"\"ClientId\"\": \"\"abc\"\"",
			"}\"",
			"2026-01-01 07:54:10.126\tINFO\tMy.App.OptionsSetupService\tdone"
		});

		Assert.Equal(4, entries.Count);
		Assert.Equal(new DateTime(2026, 1, 1, 7, 54, 9, 928), entries[0].TimeStamp);
		Assert.Equal(LogEntryLevel.Debug, entries[0].Level);
		Assert.Equal("My.App.OpcService", entries[0].LoggerName);
		Assert.Equal("add new channel for endpoint host:4840", entries[0].Message);
		Assert.Equal("add new handler \"Watchdog_V1\"", entries[1].Message);
		Assert.Equal(
			string.Join(Environment.NewLine, "{", "  \"Enabled\": true,", "  \"ClientId\": \"abc\"", "}"),
			entries[2].Message);
		Assert.Equal(LogEntryLevel.Info, entries[3].Level);
		Assert.Equal("done", entries[3].Message);
	}

	[Fact]
	public async Task ParseFileAsync_TextFile_SetsLineNumberOfFirstLineOfEachEntry()
	{
		var entries = await ParseAsync(new[]
		{
			"# header",
			PipeLine(1),
			"   at Foo.Bar()",
			"   at Foo.Baz()",
			PipeLine(2)
		});

		Assert.Equal(new object?[] { 2, 5 }, entries.Select(FileLine));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ParseFileAsync_FileLargerThanOneProgressInterval_ParsesAllEntries(bool hasHeaderLine)
	{
		var lines = Enumerable.Range(1, 2500).Select(i => PipeLine(i));
		if (hasHeaderLine)
			lines = lines.Prepend("# header");

		var entries = await ParseAsync(lines);

		Assert.Equal(2500, entries.Count);
		Assert.Equal("Message number 1", entries[0].Message);
		Assert.Equal("Message number 2500", entries[^1].Message);
	}

	[Fact]
	public async Task ParseFileAsync_MultiLineEntryAtLine1000_KeepsAllItsLines()
	{
		var lines = Enumerable.Range(1, 1000).Select(i => PipeLine(i))
			.Concat(new[] { "   at Foo.Bar()", "   at Foo.Baz()", "   at Foo.Qux()" })
			.Concat(Enumerable.Range(1001, 10).Select(i => PipeLine(i)));

		var entries = await ParseAsync(lines);

		Assert.Equal(1010, entries.Count);
		Assert.Equal(
			string.Join(Environment.NewLine, "Message number 1000", "   at Foo.Bar()", "   at Foo.Baz()", "   at Foo.Qux()"),
			entries[999].Message);
	}

	[Fact]
	public async Task ParseFileAsync_SeparatorInsideMessage_KeepsWholeMessage()
	{
		var lines = Enumerable.Range(1, 20).Select(i => PipeLine(i)).Append(PipeLine(21, "left part | right part"));

		var entries = await ParseAsync(lines);

		Assert.Equal("left part | right part 21", entries[^1].Message);
	}

	[Fact]
	public async Task ParseFileAsync_UnrecognisedTimestampFormat_StillReturnsEntries()
	{
		var lines = Enumerable.Range(1, 50).Select(i => $"T+{i:D7};INFO;My.Logger.Name;Message text number {i}")
			.Append("   at Foo.Bar()");

		var entries = await ParseAsync(lines);

		Assert.Equal(50, entries.Count);
		Assert.Equal("My.Logger.Name", entries[0].LoggerName);
		Assert.Equal("Message text number 1", entries[0].Message);
		Assert.EndsWith("   at Foo.Bar()", entries[^1].Message);
	}

	[Fact]
	public async Task ParseFileAsync_GermanTimestampFormat_ParsesTimestampAndColumns()
	{
		var lines = Enumerable.Range(1, 50).Select(i => $"27.11.2025 14:53:05;INFO;My.Logger.Name;Message text number {i}");

		var entries = await ParseAsync(lines);

		Assert.Equal(50, entries.Count);
		Assert.Equal(new DateTime(2025, 11, 27, 14, 53, 5), entries[0].TimeStamp);
		Assert.Equal("My.Logger.Name", entries[0].LoggerName);
	}
}
