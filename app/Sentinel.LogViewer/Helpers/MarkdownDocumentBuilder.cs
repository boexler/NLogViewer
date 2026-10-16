using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Sentinel.LogViewer.Helpers;

/// <summary>
/// Builds a <see cref="FlowDocument"/> from Markdown. The colours are the brushes of the theme, so the document follows it.
/// </summary>
public static class MarkdownDocumentBuilder
{
	private const string CodeFont = "Consolas";

	public static FlowDocument Build(string markdown)
	{
		var document = new FlowDocument
		{
			FontFamily = new FontFamily("Segoe UI"),
			FontSize = 13,
			PagePadding = new Thickness(0),
			LineHeight = 20,
			Background = Brushes.Transparent
		};
		document.SetResourceReference(TextElement.ForegroundProperty, "ThemeTextPrimary");

		// Items of a list are collected, so that the items below an item become a list in that item.
		var lists = new Stack<(int Level, bool Ordered, List List)>();

		foreach (var block in MarkdownParser.ParseBlocks(markdown))
		{
			if (block is not ListItemBlock item)
			{
				lists.Clear();
				document.Blocks.Add(CreateBlock(block));
				continue;
			}

			// A list of the other kind on the same level (numbers after bullets) is a list of its own.
			while (lists.Count > 0 && (lists.Peek().Level > item.Level || (lists.Peek().Level == item.Level && lists.Peek().Ordered != item.Ordered)))
				lists.Pop();

			if (lists.Count == 0 || lists.Peek().Level < item.Level)
			{
				var list = CreateList(item);
				var parent = lists.Count == 0 ? null : lists.Peek().List.ListItems.LastListItem;
				if (parent == null)
					document.Blocks.Add(list);
				else
					parent.Blocks.Add(list);

				lists.Push((item.Level, item.Ordered, list));
			}

			lists.Peek().List.ListItems.Add(new ListItem(CreateParagraph(item.Inlines, new Thickness(0, 1, 0, 1))));
		}

		return document;
	}

	private static List CreateList(ListItemBlock first) => new()
	{
		MarkerStyle = first.Ordered ? TextMarkerStyle.Decimal : first.Level == 0 ? TextMarkerStyle.Disc : TextMarkerStyle.Circle,
		StartIndex = first.Ordered ? Math.Max(1, first.Number) : 1,
		Margin = new Thickness(0, 2, 0, 6),
		Padding = new Thickness(first.Level == 0 ? 20 : 18, 0, 0, 0)
	};

	private static Block CreateBlock(MarkdownBlock block)
	{
		switch (block)
		{
			case HeadingBlock heading:
			{
				var paragraph = CreateParagraph(heading.Inlines, new Thickness(0, heading.Level <= 2 ? 14 : 10, 0, 4));
				paragraph.FontSize = heading.Level switch { 1 => 22, 2 => 17, 3 => 14.5, _ => 13 };
				paragraph.FontWeight = FontWeights.SemiBold;
				return paragraph;
			}
			case QuoteBlock quote:
			{
				var paragraph = CreateParagraph(quote.Inlines, new Thickness(0, 4, 0, 8));
				paragraph.BorderThickness = new Thickness(3, 0, 0, 0);
				paragraph.Padding = new Thickness(10, 2, 0, 2);
				paragraph.SetResourceReference(Block.BorderBrushProperty, "ThemeBorderStrong");
				paragraph.SetResourceReference(TextElement.ForegroundProperty, "ThemeTextSecondary");
				return paragraph;
			}
			case CodeBlock code:
			{
				var paragraph = new Paragraph(new Run(code.Text))
				{
					FontFamily = new FontFamily(CodeFont),
					FontSize = 12,
					Padding = new Thickness(10, 8, 10, 8),
					Margin = new Thickness(0, 4, 0, 8),
					BorderThickness = new Thickness(1)
				};
				paragraph.SetResourceReference(Block.BackgroundProperty, "ThemeSurfaceAlt");
				paragraph.SetResourceReference(Block.BorderBrushProperty, "ThemeBorder");
				return paragraph;
			}
			case RuleBlock:
			{
				var line = new System.Windows.Controls.Border { Height = 1, Margin = new Thickness(0, 8, 0, 8) };
				line.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "ThemeBorder");
				return new BlockUIContainer(line);
			}
			case ParagraphBlock paragraph:
				return CreateParagraph(paragraph.Inlines, new Thickness(0, 2, 0, 8));
			default:
				return new Paragraph();
		}
	}

	private static Paragraph CreateParagraph(IEnumerable<MarkdownInline> pieces, Thickness margin)
	{
		var paragraph = new Paragraph { Margin = margin };
		foreach (var piece in pieces)
			paragraph.Inlines.Add(CreateInline(piece));

		return paragraph;
	}

	private static Inline CreateInline(MarkdownInline piece)
	{
		var run = new Run(piece.Text);
		if (piece.Style.HasFlag(MarkdownStyle.Bold))
			run.FontWeight = FontWeights.SemiBold;
		if (piece.Style.HasFlag(MarkdownStyle.Italic))
			run.FontStyle = FontStyles.Italic;
		if (piece.Style.HasFlag(MarkdownStyle.Code))
		{
			run.FontFamily = new FontFamily(CodeFont);
			run.FontSize = 12;
			run.SetResourceReference(TextElement.BackgroundProperty, "ThemeSurfaceAlt");
		}

		if (piece.Url == null)
			return run;

		var link = new Hyperlink(run);
		link.SetResourceReference(TextElement.ForegroundProperty, "ThemeAccent");
		if (Uri.TryCreate(piece.Url, UriKind.Absolute, out var address) && address.Scheme is "http" or "https")
		{
			link.NavigateUri = address;
			link.ToolTip = piece.Url;
			link.RequestNavigate += (_, e) =>
			{
				Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
				e.Handled = true;
			};
		}

		return link;
	}
}
