using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Updates;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// Tests for <see cref="ChangelogService"/>: the releases become the changelog, which is kept on disk for the times without a network
/// </summary>
public class ChangelogServiceTests : IDisposable
{
	private const string RealPrereleaseBody =
		"> **Prerelease** of the [v5.0 – Sentinel.LogViewer platform](https://github.com/boexler/NLogViewer/milestone/1) milestone. The library API may still change before 5.0.0.\n" +
		">\n" +
		"> - Chocolatey: `choco upgrade Sentinel.LogViewer --pre -y` (package version `5.0.0-beta2`)\n" +
		"> - NuGet: `Sentinel.LogViewer.Wpf` version `5.0.0-beta.2` (enable prereleases)\n" +
		">\n" +
		"> This is the first version with the built-in updater.\n" +
		"\n" +
		"## 🚀 Features\n" +
		"- feat(app): check GitHub Releases for updates by @boexler in issue [139](https://github.com/boexler/NLogViewer/issues/139)\n";

	private readonly string _directory = Path.Combine(Path.GetTempPath(), "nlv_changelog_" + Guid.NewGuid().ToString("N"));

	public ChangelogServiceTests()
	{
		Directory.CreateDirectory(_directory);
	}

	public void Dispose() => Directory.Delete(_directory, recursive: true);

	private string CachePath => Path.Combine(_directory, "changelog.json");

	private static GitHubRelease Release(string tag, string? body = "notes", bool prerelease = false, bool draft = false, string? published = "2026-10-14T10:00:00Z") =>
		new()
		{
			TagName = tag,
			HtmlUrl = "https://github.com/boexler/NLogViewer/releases/tag/" + tag,
			Body = body,
			Prerelease = prerelease,
			Draft = draft,
			PublishedAt = published == null ? null : DateTimeOffset.Parse(published)
		};

	private static ReleaseVersion Version(string text)
	{
		Assert.True(ReleaseVersion.TryParse(text, out var version));
		return version;
	}

	private sealed class FakeUpdateService(Func<Task<List<GitHubRelease>>> releases) : UpdateService
	{
		public override Task<List<GitHubRelease>> GetReleasesAsync(CancellationToken cancellationToken = default) => releases();
	}

	[Fact]
	public void Parse_SortsNewestFirst_AndLeavesOutDraftsAndBuildsWithoutAVersion()
	{
		var entries = ChangelogService.Parse(new[]
		{
			Release("v5.0.0-beta.1", prerelease: true),
			Release("v4.0.0"),
			Release("v5.0.0-beta.2", prerelease: true),
			Release("dev-3.2.0-dev.4.1", prerelease: true),
			Release("v6.0.0", draft: true),
			Release("v3.2.0")
		});

		Assert.Equal(new[] { "5.0.0-beta.2", "5.0.0-beta.1", "4.0.0", "3.2.0" }, entries.Select(entry => entry.Version.ToString()));
		Assert.Equal(new[] { true, true, false, false }, entries.Select(entry => entry.IsPrerelease));
	}

	[Fact]
	public void Parse_KeepsTheDateTheAddressAndTheNotes()
	{
		var entry = Assert.Single(ChangelogService.Parse(new[] { Release("v4.0.0", body: "## Notes\n- one", published: "2026-09-15T14:59:31Z") }));

		Assert.Equal(DateTimeOffset.Parse("2026-09-15T14:59:31Z"), entry.PublishedAt);
		Assert.Equal("https://github.com/boexler/NLogViewer/releases/tag/v4.0.0", entry.Url);
		Assert.Equal("## Notes\n- one", entry.Markdown);
	}

	[Fact]
	public void Parse_ToleratesAReleaseWithoutNotesOrDate()
	{
		var entry = Assert.Single(ChangelogService.Parse(new[] { Release("v4.0.0", body: null, published: null) }));

		Assert.Equal(string.Empty, entry.Markdown);
		Assert.Null(entry.PublishedAt);
	}

	[Fact]
	public void Parse_ListsAVersionOnce()
	{
		var entries = ChangelogService.Parse(new[] { Release("v4.0.0"), Release("4.0.0") });

		Assert.Single(entries);
	}

	[Fact]
	public void StripInstallationNotes_RemovesTheBlockAtTheTopThatTellsHowToInstall()
	{
		var notes = ChangelogService.StripInstallationNotes(RealPrereleaseBody);

		Assert.StartsWith("## 🚀 Features", notes);
		Assert.DoesNotContain("Chocolatey", notes);
		Assert.Contains("issue [139]", notes);
	}

	[Theory]
	[InlineData("> A quote without hints about installing\n\nText", "> A quote without hints about installing\n\nText")]
	[InlineData("Text first\n\n> **Chocolatey** later in the text", "Text first\n\n> **Chocolatey** later in the text")]
	[InlineData("## Notes\n- one", "## Notes\n- one")]
	[InlineData("", "")]
	public void StripInstallationNotes_LeavesEverythingElseAlone(string markdown, string expected)
	{
		Assert.Equal(expected, ChangelogService.StripInstallationNotes(markdown));
	}

	[Fact]
	public void VersionsBetween_AreThoseAfterThePreviousVersionUpToTheCurrentOne_NewestFirst()
	{
		var entries = ChangelogService.Parse(new[]
		{
			Release("v5.0.0-beta.1"), Release("v5.0.0-beta.2"), Release("v5.0.0-beta.3"), Release("v5.0.0-beta.4"), Release("v4.0.0")
		});

		var between = ChangelogService.VersionsBetween(entries, Version("5.0.0-beta.1"), Version("5.0.0-beta.3"));

		Assert.Equal(new[] { "5.0.0-beta.3", "5.0.0-beta.2" }, between.Select(entry => entry.Version.ToString()));
	}

	[Fact]
	public async Task LoadAsync_ReturnsTheReleasesOfGitHub_AndSavesThem()
	{
		var service = new ChangelogService(new FakeUpdateService(() => Task.FromResult(new List<GitHubRelease> { Release("v4.0.0"), Release("v5.0.0-beta.1") })), CachePath);

		var list = await service.LoadAsync();

		Assert.False(list.IsFromCache);
		Assert.Equal(new[] { "5.0.0-beta.1", "4.0.0" }, list.Entries.Select(entry => entry.Version.ToString()));
		Assert.True(File.Exists(CachePath));
	}

	[Fact]
	public async Task LoadAsync_WithoutANetwork_ReturnsTheSavedList_WithTheTimeItWasSaved()
	{
		var online = new ChangelogService(new FakeUpdateService(() => Task.FromResult(new List<GitHubRelease> { Release("v4.0.0", body: "## Saved notes") })), CachePath);
		var saved = await online.LoadAsync();
		var offline = new ChangelogService(new FakeUpdateService(() => throw new HttpRequestException("no network")), CachePath);

		var list = await offline.LoadAsync();

		Assert.True(list.IsFromCache);
		Assert.Equal("## Saved notes", Assert.Single(list.Entries).Markdown);
		Assert.NotNull(list.SavedAt);
		Assert.True(Math.Abs((list.SavedAt!.Value - saved.SavedAt!.Value).TotalSeconds) < 5);
	}

	[Fact]
	public async Task LoadAsync_WhenTheApiLimitIsReached_ReturnsTheSavedList_InsteadOfThrowing()
	{
		var online = new ChangelogService(new FakeUpdateService(() => Task.FromResult(new List<GitHubRelease> { Release("v4.0.0", body: "## Saved notes") })), CachePath);
		await online.LoadAsync();
		var limited = new ChangelogService(new FakeUpdateService(() => throw new GitHubRateLimitException(DateTimeOffset.Now.AddMinutes(20))), CachePath);

		var list = await limited.LoadAsync();

		Assert.True(list.IsFromCache);
		Assert.Equal("## Saved notes", Assert.Single(list.Entries).Markdown);
	}

	[Fact]
	public async Task LoadAsync_WithoutANetworkAndWithoutASavedList_ReturnsAnEmptyList()
	{
		var service = new ChangelogService(new FakeUpdateService(() => throw new HttpRequestException("no network")), CachePath);

		var list = await service.LoadAsync();

		Assert.Empty(list.Entries);
		Assert.True(list.IsFromCache);
		Assert.Null(list.SavedAt);
	}

	[Fact]
	public async Task LoadAsync_DoesNotHideACancellation()
	{
		var service = new ChangelogService(new FakeUpdateService(() => throw new TaskCanceledException()), CachePath);
		using var source = new CancellationTokenSource();
		source.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.LoadAsync(source.Token));
	}

	[Fact]
	public void LoadSaved_IgnoresAFileThatIsNotTheSavedList()
	{
		File.WriteAllText(CachePath, "not json at all");
		var service = new ChangelogService(new FakeUpdateService(() => Task.FromResult(new List<GitHubRelease>())), CachePath);

		Assert.Empty(service.LoadSaved().Entries);
	}

	[Fact]
	public async Task TheSavedList_GoesIntoTheFolderOfTheConfiguration()
	{
		using var environment = new Sentinel.LogViewer.Tests.ViewModels.SettingsTestEnvironment();
		var service = new ChangelogService(new FakeUpdateService(() => Task.FromResult(new List<GitHubRelease> { Release("v4.0.0") })), environment.Configuration);

		await service.LoadAsync();

		Assert.True(File.Exists(Path.Combine(environment.Directory, "changelog.json")));
	}
}
