using System.Linq;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Parsers;
using Xunit;

namespace Sentinel.LogViewer.Tests.Parsers;

/// <summary>
/// Tests that the columns of a text file that are not mapped are kept as properties
/// </summary>
public class PlainTextParserColumnsTests
{
	private static TextFileFormat Format(bool header) => new()
	{
		Separator = ";",
		HasHeaderLine = header,
		StartLineIndex = header ? 1 : 0,
		ColumnCount = 6,
		ColumnMapping = new ColumnMapping { TimestampColumn = 0, LevelColumn = 1, LoggerColumn = 2, MessageColumn = 5 }
	};

	[Fact]
	public void TheColumnsThatAreNotMapped_BecomePropertiesNamedAfterTheHeaderLine()
	{
		var lines = new[]
		{
			"Time;Level;Logger;Thread;Machine;Text",
			"2025-12-18 15:11:48;ERROR;Acme.Shop;worker-3;NB01;payment failed",
			"2025-12-18 15:11:49;INFO;Acme.Shop;worker-4;;done"
		};

		var entries = new PlainTextParser().Parse(lines, Format(header: true));

		Assert.Equal(2, entries.Count);
		Assert.Equal("payment failed", entries[0].Message);
		Assert.Equal("worker-3", entries[0].Properties["Thread"]);
		Assert.Equal("NB01", entries[0].Properties["Machine"]);
		Assert.DoesNotContain("Time", entries[0].Properties.Keys);
		Assert.DoesNotContain("Machine", entries[1].Properties.Keys);
	}

	[Fact]
	public void WithoutAHeaderLine_TheColumnsAreCalledColumnN()
	{
		var lines = new[] { "2025-12-18 15:11:48;ERROR;Acme.Shop;worker-3;NB01;payment failed" };

		var entry = Assert.Single(new PlainTextParser().Parse(lines, Format(header: false)));

		Assert.Equal(new[] { "Column 4", "Column 5" }, entry.Properties.Keys.Where(name => name.StartsWith("Column")).OrderBy(name => name));
		Assert.Equal("worker-3", entry.Properties["Column 4"]);
	}

	[Fact]
	public void AFileWithoutExtraColumns_HasNoProperties_ButTheLineNumber()
	{
		var format = new TextFileFormat
		{
			Separator = ";",
			ColumnCount = 4,
			ColumnMapping = new ColumnMapping { TimestampColumn = 0, LevelColumn = 1, LoggerColumn = 2, MessageColumn = 3 }
		};

		var entry = Assert.Single(new PlainTextParser().Parse(new[] { "2025-12-18 15:11:48;INFO;Acme;hello" }, format));

		Assert.Equal(new[] { PlainTextParser.FileLinePropertyName }, entry.Properties.Keys);
	}
}
