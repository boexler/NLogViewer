using Sentinel.LogViewer.Parsers;
using Xunit;

namespace Sentinel.LogViewer.Tests.Parsers;

/// <summary>
/// Unit tests for <see cref="TextLineSplitter"/>
/// </summary>
public class TextLineSplitterTests
{
	[Fact]
	public void Split_QuotedField_RemovesQuotesAndKeepsSeparatorAndLineBreaks()
	{
		var fields = TextLineSplitter.Split("a\t\"say \"\"hi\"\"\tthere\nagain\"\tc", "\t", quotedFields: true);

		Assert.Equal(new[] { "a", "say \"hi\"\tthere\nagain", "c" }, fields);
	}

	[Fact]
	public void Split_TextAfterClosingQuote_TakesFieldLiterally()
	{
		var fields = TextLineSplitter.Split("a\t\"Hello\" he said\tc", "\t", quotedFields: true);

		Assert.Equal(new[] { "a", "\"Hello\" he said", "c" }, fields);
	}

	[Theory]
	[InlineData(" | ", "a | b | c | d")]
	[InlineData(TextLineSplitter.SpacesSeparator, "a   b c | d")]
	public void Split_MaxColumns_LastColumnTakesRestOfLine(string separator, string line)
	{
		var fields = TextLineSplitter.Split(line, separator, maxColumns: 2);

		Assert.Equal(2, fields.Length);
		Assert.Equal("a", fields[0]);
		Assert.EndsWith("c | d", fields[1]);
	}

	[Theory]
	[InlineData("a\t\"open", false, true)]
	[InlineData("a\t\"closed\"", false, false)]
	[InlineData("a\tinner \" quote", false, false)]
	[InlineData("still \"\"open\"\"", true, true)]
	[InlineData("closed\"\tb", true, false)]
	public void EndsInsideQuotedField_TracksQuoteStateAcrossLines(string line, bool startsInside, bool expected)
	{
		Assert.Equal(expected, TextLineSplitter.EndsInsideQuotedField(line, "\t", startsInside));
	}
}
