using System.Linq;
using Sentinel.LogViewer.Helpers;
using Xunit;

namespace Sentinel.LogViewer.Tests.Helpers;

/// <summary>
/// Tests for <see cref="MarkdownParser"/>: the Markdown that the release notes of GitHub use
/// </summary>
public class MarkdownParserTests
{
	private static string Text(MarkdownBlock block) => block switch
	{
		HeadingBlock heading => string.Concat(heading.Inlines.Select(piece => piece.Text)),
		ParagraphBlock paragraph => string.Concat(paragraph.Inlines.Select(piece => piece.Text)),
		ListItemBlock item => string.Concat(item.Inlines.Select(piece => piece.Text)),
		QuoteBlock quote => string.Concat(quote.Inlines.Select(piece => piece.Text)),
		CodeBlock code => code.Text,
		_ => string.Empty
	};

	[Fact]
	public void Headings_KeepTheirLevel()
	{
		var blocks = MarkdownParser.ParseBlocks("# One\n\n## 🚀 Features\n\n### Fixes ###\n");

		Assert.Equal(new[] { 1, 2, 3 }, blocks.Cast<HeadingBlock>().Select(heading => heading.Level));
		Assert.Equal(new[] { "One", "🚀 Features", "Fixes" }, blocks.Select(Text));
	}

	[Fact]
	public void Paragraphs_JoinTheirLines_AndAreSeparatedByBlankLines()
	{
		var blocks = MarkdownParser.ParseBlocks("First line\nsecond line\n\nNext paragraph");

		Assert.Equal(new[] { "First line second line", "Next paragraph" }, blocks.Cast<ParagraphBlock>().Select(Text));
	}

	[Fact]
	public void Lists_KeepTheKindTheNumberAndTheLevelOfTheirItems()
	{
		var blocks = MarkdownParser.ParseBlocks("- one\n  - nested\n* two\n\n1. first\n2) second");

		var items = blocks.Cast<ListItemBlock>().ToList();
		Assert.Equal(new[] { "one", "nested", "two", "first", "second" }, items.Select(Text));
		Assert.Equal(new[] { 0, 1, 0, 0, 0 }, items.Select(item => item.Level));
		Assert.Equal(new[] { false, false, false, true, true }, items.Select(item => item.Ordered));
		Assert.Equal(new[] { 1, 2 }, items.Where(item => item.Ordered).Select(item => item.Number));
	}

	[Fact]
	public void AnIndentedLine_ContinuesTheItemAbove()
	{
		var blocks = MarkdownParser.ParseBlocks("- an item\n  that goes on\n- the next");

		Assert.Equal(new[] { "an item that goes on", "the next" }, blocks.Cast<ListItemBlock>().Select(Text));
	}

	[Fact]
	public void CodeBlocks_KeepTheirLinesAsTheyAre()
	{
		var blocks = MarkdownParser.ParseBlocks("Before\n\n```powershell\nchoco upgrade Sentinel.LogViewer --pre -y\n# a comment\n```\n\nAfter");

		Assert.Equal("choco upgrade Sentinel.LogViewer --pre -y\n# a comment".Replace("\n", System.Environment.NewLine), Text(blocks[1]));
		Assert.IsType<CodeBlock>(blocks[1]);
		Assert.Equal(3, blocks.Count);
	}

	[Fact]
	public void QuotesAndRules_AreBlocksOfTheirOwn()
	{
		var blocks = MarkdownParser.ParseBlocks("> **Prerelease** of the milestone\n\n---\n\nText");

		Assert.IsType<QuoteBlock>(blocks[0]);
		Assert.IsType<RuleBlock>(blocks[1]);
		Assert.IsType<ParagraphBlock>(blocks[2]);
	}

	[Fact]
	public void Inlines_KnowBoldItalicAndCode()
	{
		var pieces = MarkdownParser.ParseInlines("a **bold** and *italic* and `code` and __also bold__ and _also italic_");

		Assert.Equal(new[] { "a ", "bold", " and ", "italic", " and ", "code", " and ", "also bold", " and ", "also italic" }, pieces.Select(piece => piece.Text));
		Assert.Equal(new[] { MarkdownStyle.Bold, MarkdownStyle.Italic, MarkdownStyle.Code, MarkdownStyle.Bold, MarkdownStyle.Italic },
			pieces.Where(piece => piece.Style != MarkdownStyle.None).Select(piece => piece.Style));
	}

	[Fact]
	public void Inlines_CanBeBoldAndItalicAtOnce()
	{
		var pieces = MarkdownParser.ParseInlines("**bold and *italic* inside**");

		Assert.Equal(new[] { "bold and ", "italic", " inside" }, pieces.Select(piece => piece.Text));
		Assert.Equal(new[] { MarkdownStyle.Bold, MarkdownStyle.Bold | MarkdownStyle.Italic, MarkdownStyle.Bold }, pieces.Select(piece => piece.Style));
	}

	[Theory]
	[InlineData("snake_case_name and my_var")]
	[InlineData("2 * 3 = 6 and 4 * 5 = 20")]
	[InlineData("a lone * star and a lone _ mark")]
	public void Inlines_DoNotTakeNamesOrSumsForEmphasis(string text)
	{
		var pieces = MarkdownParser.ParseInlines(text);

		Assert.Equal(text, string.Concat(pieces.Select(piece => piece.Text)));
		Assert.All(pieces, piece => Assert.Equal(MarkdownStyle.None, piece.Style));
	}

	[Fact]
	public void Links_KeepTheirTextAndTheirAddress()
	{
		var pieces = MarkdownParser.ParseInlines("in issue [139](https://github.com/boexler/NLogViewer/issues/139) and [**bold** link](https://example.com/a_b)");

		Assert.Equal(new[] { "in issue ", "139", " and ", "bold", " link" }, pieces.Select(piece => piece.Text));
		Assert.Equal(new[] { "https://github.com/boexler/NLogViewer/issues/139", "https://example.com/a_b", "https://example.com/a_b" },
			pieces.Where(piece => piece.Url != null).Select(piece => piece.Url));
		Assert.Equal(MarkdownStyle.Bold, pieces.Single(piece => piece.Text == "bold").Style);
	}

	[Fact]
	public void AddressesInTheText_AreLinks_WithoutTheClosingPunctuation()
	{
		var pieces = MarkdownParser.ParseInlines("Compare (https://github.com/boexler/NLogViewer/compare/v1...v2). Done.");

		var link = Assert.Single(pieces, piece => piece.Url != null);
		Assert.Equal("https://github.com/boexler/NLogViewer/compare/v1...v2", link.Url);
		Assert.Equal("). Done.", pieces[^1].Text);
	}

	[Fact]
	public void Inlines_RemoveHtmlTags_AndKeepEscapedCharacters()
	{
		var pieces = MarkdownParser.ParseInlines("one<br>two <b>bold</b> and \\*not italic\\*");

		Assert.Equal("onetwo bold and *not italic*", string.Concat(pieces.Select(piece => piece.Text)));
	}

	[Fact]
	public void ARealReleaseLine_IsReadAsItIsWritten()
	{
		var blocks = MarkdownParser.ParseBlocks(
			"## 🚀 Features\n- feat(app): check GitHub Releases for updates, with a Firefox-style About dialog under Help → About by @boexler in issue [139](https://github.com/boexler/NLogViewer/issues/139)");

		var item = Assert.IsType<ListItemBlock>(blocks[1]);
		Assert.Equal("feat(app): check GitHub Releases for updates, with a Firefox-style About dialog under Help → About by @boexler in issue ", item.Inlines[0].Text);
		Assert.Equal("139", item.Inlines[1].Text);
		Assert.Equal("https://github.com/boexler/NLogViewer/issues/139", item.Inlines[1].Url);
	}

	[Fact]
	public void EmptyMarkdown_HasNoBlocks()
	{
		Assert.Empty(MarkdownParser.ParseBlocks(string.Empty));
		Assert.Empty(MarkdownParser.ParseBlocks("\n \n"));
	}
}
