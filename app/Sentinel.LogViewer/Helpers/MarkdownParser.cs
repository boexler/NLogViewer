using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Sentinel.LogViewer.Helpers;

/// <summary>
/// How a piece of text is shown
/// </summary>
[Flags]
public enum MarkdownStyle
{
	None = 0,
	Bold = 1,
	Italic = 2,
	Code = 4
}

/// <summary>
/// A piece of text with one style, or a link when it has an address
/// </summary>
public sealed record MarkdownInline(string Text, MarkdownStyle Style = MarkdownStyle.None, string? Url = null);

public abstract record MarkdownBlock;

public sealed record HeadingBlock(int Level, IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

public sealed record ParagraphBlock(IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

/// <param name="Level">0 for an item of the list itself, 1 for an item in an item, ...</param>
public sealed record ListItemBlock(int Level, bool Ordered, int Number, IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

public sealed record QuoteBlock(IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

public sealed record CodeBlock(string Text) : MarkdownBlock;

public sealed record RuleBlock : MarkdownBlock;

/// <summary>
/// Reads the part of Markdown that the release notes of GitHub use: headings, paragraphs, bulleted and numbered lists, quotes,
/// code, rules, and in the text bold, italic, code and links. Everything else is shown as the text it is.
/// </summary>
public static class MarkdownParser
{
	private static readonly Regex Heading = new(@"^\s{0,3}(?<marks>#{1,6})\s+(?<text>.*?)\s*#*\s*$", RegexOptions.Compiled);
	private static readonly Regex Rule = new(@"^\s{0,3}(?:-{3,}|\*{3,}|_{3,})\s*$", RegexOptions.Compiled);
	private static readonly Regex ListItem = new(@"^(?<indent>\s*)(?<marker>[-*+]|\d+[.)])\s+(?<text>.*)$", RegexOptions.Compiled);
	private static readonly Regex Quote = new(@"^\s{0,3}>\s?(?<text>.*)$", RegexOptions.Compiled);
	private static readonly Regex Fence = new(@"^\s{0,3}(```|~~~)", RegexOptions.Compiled);
	private static readonly Regex Html = new(@"<(?!/?(?:https?:))[^>\n]+>", RegexOptions.Compiled);

	/// <summary>
	/// Splits Markdown into blocks
	/// </summary>
	public static IReadOnlyList<MarkdownBlock> ParseBlocks(string markdown)
	{
		var blocks = new List<MarkdownBlock>();
		var paragraph = new List<string>();
		var lines = markdown.Replace("\r\n", "\n").Split('\n');

		void EndParagraph()
		{
			if (paragraph.Count > 0)
				blocks.Add(new ParagraphBlock(ParseInlines(string.Join(" ", paragraph))));

			paragraph.Clear();
		}

		for (var i = 0; i < lines.Length; i++)
		{
			var line = lines[i];
			if (Fence.IsMatch(line))
			{
				EndParagraph();
				var code = new StringBuilder();
				for (i++; i < lines.Length && !Fence.IsMatch(lines[i]); i++)
					code.AppendLine(lines[i]);

				blocks.Add(new CodeBlock(code.ToString().TrimEnd('\r', '\n')));
				continue;
			}

			if (string.IsNullOrWhiteSpace(line))
			{
				EndParagraph();
				continue;
			}

			var heading = Heading.Match(line);
			if (heading.Success)
			{
				EndParagraph();
				blocks.Add(new HeadingBlock(heading.Groups["marks"].Length, ParseInlines(heading.Groups["text"].Value)));
				continue;
			}

			if (Rule.IsMatch(line))
			{
				EndParagraph();
				blocks.Add(new RuleBlock());
				continue;
			}

			var quote = Quote.Match(line);
			if (quote.Success)
			{
				EndParagraph();
				if (quote.Groups["text"].Value.Trim().Length > 0)
					blocks.Add(new QuoteBlock(ParseInlines(quote.Groups["text"].Value)));

				continue;
			}

			var item = ListItem.Match(line);
			if (item.Success)
			{
				EndParagraph();
				var marker = item.Groups["marker"].Value;
				var ordered = char.IsDigit(marker[0]);
				var level = item.Groups["indent"].Value.Replace("\t", "    ").Length / 2;
				blocks.Add(new ListItemBlock(level, ordered, ordered ? int.Parse(marker.TrimEnd('.', ')')) : 0, ParseInlines(item.Groups["text"].Value)));
				continue;
			}

			// An indented line after a list item continues that item.
			if (char.IsWhiteSpace(line[0]) && paragraph.Count == 0 && blocks.Count > 0 && blocks[^1] is ListItemBlock last)
			{
				blocks[^1] = last with { Inlines = last.Inlines.Concat(new[] { new MarkdownInline(" ") }).Concat(ParseInlines(line.Trim())).ToList() };
				continue;
			}

			paragraph.Add(line.Trim());
		}

		EndParagraph();
		return blocks;
	}

	/// <summary>
	/// Splits the text of a block into pieces: bold, italic, code and links
	/// </summary>
	public static IReadOnlyList<MarkdownInline> ParseInlines(string text) => Merge(Parse(Html.Replace(text, string.Empty), MarkdownStyle.None));

	private static List<MarkdownInline> Parse(string text, MarkdownStyle style)
	{
		var result = new List<MarkdownInline>();
		var plain = new StringBuilder();

		void Flush()
		{
			if (plain.Length > 0)
				result.Add(new MarkdownInline(plain.ToString(), style));

			plain.Clear();
		}

		for (var i = 0; i < text.Length; i++)
		{
			var c = text[i];
			if (c == '\\' && i + 1 < text.Length && "\\`*_{}[]()#+-.!>".Contains(text[i + 1]))
			{
				plain.Append(text[++i]);
				continue;
			}

			if (c == '`')
			{
				var end = text.IndexOf('`', i + 1);
				if (end > i)
				{
					Flush();
					result.Add(new MarkdownInline(text[(i + 1)..end], style | MarkdownStyle.Code));
					i = end;
					continue;
				}
			}

			if ((c == '*' || c == '_') && i + 1 < text.Length && text[i + 1] == c)
			{
				var end = text.IndexOf(new string(c, 2), i + 2, StringComparison.Ordinal);
				if (end > i + 2 && (c == '*' || IsEmphasisStart(text, i)))
				{
					Flush();
					result.AddRange(Parse(text[(i + 2)..end], style | MarkdownStyle.Bold));
					i = end + 1;
					continue;
				}
			}

			if ((c == '*' || c == '_') && i + 1 < text.Length && !char.IsWhiteSpace(text[i + 1]) && IsEmphasisStart(text, i))
			{
				var end = FindEmphasisEnd(text, c, i + 1);
				if (end > i + 1)
				{
					Flush();
					result.AddRange(Parse(text[(i + 1)..end], style | MarkdownStyle.Italic));
					i = end;
					continue;
				}
			}

			if (c == '[')
			{
				var close = text.IndexOf("](", i + 1, StringComparison.Ordinal);
				var end = close < 0 ? -1 : text.IndexOf(')', close + 2);
				if (close > i && end > close)
				{
					Flush();
					var url = text[(close + 2)..end].Trim();
					var label = Parse(text[(i + 1)..close], style);
					result.AddRange(label.Select(piece => piece with { Url = url }));
					i = end;
					continue;
				}
			}

			if ((c == 'h' || c == 'H') && IsAddress(text, i))
			{
				var end = i;
				while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != ')' && text[end] != '>')
					end++;

				var address = text[i..end].TrimEnd('.', ',', ';', ':', '!', '?');
				Flush();
				result.Add(new MarkdownInline(address, style, address));
				i += address.Length - 1;
				continue;
			}

			plain.Append(c);
		}

		Flush();
		return result;
	}

	/// <summary>
	/// An underscore inside a word (snake_case) is no emphasis; an asterisk is
	/// </summary>
	private static bool IsEmphasisStart(string text, int index) =>
		text[index] == '*' || index == 0 || !char.IsLetterOrDigit(text[index - 1]);

	private static int FindEmphasisEnd(string text, char mark, int from)
	{
		for (var i = from; i < text.Length; i++)
		{
			if (text[i] != mark || char.IsWhiteSpace(text[i - 1]))
				continue;

			if (mark == '*' || i + 1 == text.Length || !char.IsLetterOrDigit(text[i + 1]))
				return i;
		}

		return -1;
	}

	private static bool IsAddress(string text, int index) =>
		string.Compare(text, index, "http://", 0, 7, StringComparison.OrdinalIgnoreCase) == 0 ||
		string.Compare(text, index, "https://", 0, 8, StringComparison.OrdinalIgnoreCase) == 0;

	/// <summary>
	/// Joins neighbouring pieces that look the same
	/// </summary>
	private static IReadOnlyList<MarkdownInline> Merge(List<MarkdownInline> pieces)
	{
		var merged = new List<MarkdownInline>();
		foreach (var piece in pieces)
		{
			if (merged.Count > 0 && merged[^1].Style == piece.Style && merged[^1].Url == piece.Url)
				merged[^1] = merged[^1] with { Text = merged[^1].Text + piece.Text };
			else
				merged.Add(piece);
		}

		return merged;
	}
}
