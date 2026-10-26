using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Sentinel.LogViewer.Helpers;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Updates;
using Sentinel.LogViewer.Tests.ViewModels;
using Sentinel.LogViewer.ViewModels;
using Xunit;
using static Sentinel.LogViewer.Tests.WpfTestHelper;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for the changelog window and for the document that shows the release notes
/// </summary>
[Collection(WpfCollection.Name)]
public class ChangelogWindowTests
{
	private readonly WpfStaContextFixture _context;

	public ChangelogWindowTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private sealed class FakeUpdateService(Func<Task<List<GitHubRelease>>> releases) : UpdateService
	{
		public override Task<List<GitHubRelease>> GetReleasesAsync(CancellationToken cancellationToken = default) => releases();
	}

	private static GitHubRelease Release(string tag, string body, bool prerelease = true) => new()
	{
		TagName = tag,
		HtmlUrl = "https://github.com/boexler/NLogViewer/releases/tag/" + tag,
		Body = body,
		Prerelease = prerelease,
		PublishedAt = DateTimeOffset.Parse("2026-10-14T10:00:00Z")
	};

	private static ReleaseVersion Version(string text)
	{
		Assert.True(ReleaseVersion.TryParse(text, out var version));
		return version;
	}

	private static string TextOf(Block block) => new TextRange(block.ContentStart, block.ContentEnd).Text.Trim();

	[Fact]
	public void Document_ShowsHeadingsParagraphsListsAndLinks()
	{
		_context.RunOnSta(() =>
		{
			var document = MarkdownDocumentBuilder.Build(
				"## 🚀 Features\n\nSome **bold** text with `code`.\n\n- first\n  - nested\n- second with [a link](https://example.com/page)\n\n1. one\n2. two\n\n> a quote\n\n```\ncode line\n```\n");

			var blocks = document.Blocks.ToList();
			Assert.Equal(new[] { "Paragraph", "Paragraph", "List", "List", "Paragraph", "Paragraph" }, blocks.Select(block => block.GetType().Name));
			Assert.Equal("🚀 Features", TextOf(blocks[0]));
			Assert.Equal(FontWeights.SemiBold, ((Paragraph)blocks[0]).FontWeight);

			var bullets = (List)blocks[2];
			Assert.Equal(TextMarkerStyle.Disc, bullets.MarkerStyle);
			Assert.Equal(2, bullets.ListItems.Count);
			var nested = Assert.IsType<List>(bullets.ListItems.First().Blocks.Last());
			Assert.Equal("nested", ((Run)nested.ListItems.Single().Blocks.Cast<Paragraph>().Single().Inlines.Single()).Text);

			Assert.Equal(TextMarkerStyle.Decimal, ((List)blocks[3]).MarkerStyle);

			var link = bullets.ListItems.Last().Blocks.OfType<Paragraph>().Single().Inlines.OfType<Hyperlink>().Single();
			Assert.Equal(new Uri("https://example.com/page"), link.NavigateUri);
			Assert.Equal("a link", new TextRange(link.ContentStart, link.ContentEnd).Text);
		});
	}

	[Fact]
	public void Document_UsesTheBrushesOfTheTheme_SoItFollowsTheTheme()
	{
		_context.RunOnSta(() =>
		{
			var document = MarkdownDocumentBuilder.Build("Text with [a link](https://example.com) and `code`");
			var paragraph = (Paragraph)document.Blocks.Single();
			var link = paragraph.Inlines.OfType<Hyperlink>().Single();

			var window = new Window { Content = new FlowDocumentScrollViewer { Document = document } };
			try
			{
				Assert.IsType<SolidColorBrush>(document.Foreground);
				Assert.Equal(Application.Current.FindResource("ThemeTextPrimary"), document.Foreground);
				Assert.Equal(Application.Current.FindResource("ThemeAccent"), link.Foreground);

				new Sentinel.LogViewer.Services.Theming.ThemeService(() => false).Apply(new AppConfiguration { ThemeMode = "dark" });
				Assert.Equal(Application.Current.FindResource("ThemeAccent"), link.Foreground);
				Assert.Equal(Application.Current.FindResource("ThemeTextPrimary"), document.Foreground);
			}
			finally
			{
				window.Close();
				new Sentinel.LogViewer.Services.Theming.ThemeService(() => true).Apply(new AppConfiguration());
			}
		});
	}

	[Fact]
	public void Window_LoadsTheReleases_ListsTheVersions_AndShowsTheNotesOfTheSelectedOne()
	{
		_context.RunOnSta(() =>
		{
			using var environment = new SettingsTestEnvironment();
			environment.Localization.Initialize();
			environment.Localization.SetLanguage("en");
			var configuration = environment.Saved;
			configuration.IncludePrereleaseUpdates = true;
			environment.Configuration.SaveConfiguration(configuration);
			var service = new ChangelogService(new FakeUpdateService(() => Task.FromResult(new List<GitHubRelease>
			{
				Release("v5.0.0-beta.1", "## Features\n- the first beta"),
				Release("v5.0.0-beta.2", "## Features\n- the **second** beta\n\n## Fixes\n- a fix"),
				Release("v4.0.0", "- stable four", prerelease: false)
			})), System.IO.Path.Combine(environment.Directory, "changelog.json"));
			var viewModel = new ChangelogViewModel(service, environment.Configuration, environment.Localization, Version("5.0.0-beta.2"), Version("5.0.0-beta.1"));
			var window = new ChangelogWindow(viewModel, selectSince: true) { Left = -10000, Top = -10000, ShowActivated = false };
			try
			{
				window.Show();
				for (var i = 0; i < 20 && viewModel.Items.Count == 0; i++)
				{
					Thread.Sleep(50);
					WpfTestHelper.Settle();
				}

				Assert.Equal(new[] { "Since 5.0.0-beta.1", "5.0.0-beta.2", "5.0.0-beta.1", "4.0.0" }, viewModel.Items.Select(item => item.Title));
				Assert.True(viewModel.SelectedItem?.IsSince);
				var viewer = (FlowDocumentScrollViewer)window.FindName("NotesViewer");
				Assert.Contains("5.0.0-beta.2", TextOfDocument(viewer.Document));
				Assert.Contains("a fix", TextOfDocument(viewer.Document));

				viewModel.SelectedItem = viewModel.Items.Single(item => item.Title == "4.0.0");
				WpfTestHelper.Settle();

				Assert.EndsWith("stable four", TextOfDocument(viewer.Document));
				Assert.DoesNotContain("second", TextOfDocument(viewer.Document));
				var list = Descendants<ListBox>(window).First();
				Assert.Equal(4, list.Items.Count);
				Assert.Same(viewModel.SelectedItem, list.SelectedItem);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void Window_WithoutANetworkAndWithoutASavedList_SaysThatTheChangelogIsNotAvailable()
	{
		_context.RunOnSta(() =>
		{
			using var environment = new SettingsTestEnvironment();
			environment.Localization.Initialize();
			environment.Localization.SetLanguage("en");
			var service = new ChangelogService(new FakeUpdateService(() => throw new HttpRequestException("no network")),
				System.IO.Path.Combine(environment.Directory, "changelog.json"));
			var viewModel = new ChangelogViewModel(service, environment.Configuration, environment.Localization, Version("5.0.0-beta.2"));
			var window = new ChangelogWindow(viewModel) { Left = -10000, Top = -10000, ShowActivated = false };
			try
			{
				window.Show();
				for (var i = 0; i < 20 && viewModel.IsLoading; i++)
				{
					Thread.Sleep(50);
					WpfTestHelper.Settle();
				}

				Assert.Empty(viewModel.Items);
				Assert.Contains("not available yet", viewModel.EmptyText);
				Assert.Contains(Descendants<TextBlock>(window), text => text.Text.Contains("not available yet"));
			}
			finally
			{
				window.Close();
			}
		});
	}

	private static string TextOfDocument(FlowDocument document) => new TextRange(document.ContentStart, document.ContentEnd).Text.Trim();
}
