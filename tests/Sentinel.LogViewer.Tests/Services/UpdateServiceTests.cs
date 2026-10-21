using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Updates;
using Sentinel.LogViewer.ViewModels;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// Tests for the update check: version precedence, release and file selection, and install type detection
/// </summary>
public class UpdateServiceTests
{
	private static ReleaseVersion Version(string text)
	{
		Assert.True(ReleaseVersion.TryParse(text, out var version), text);
		return version;
	}

	private static GitHubRelease Release(string tag, bool prerelease = false, bool draft = false, params string[] assets) => new()
	{
		TagName = tag,
		HtmlUrl = $"https://github.com/boexler/NLogViewer/releases/tag/{tag}",
		Prerelease = prerelease,
		Draft = draft,
		Assets = assets.Select(name => new GitHubAsset
		{
			Name = name,
			BrowserDownloadUrl = $"https://example.invalid/{name}",
			Size = 42,
			Digest = "sha256:ABC123"
		}).ToList()
	};

	private static string[] AllAssets(string version) => new[]
	{
		$"Sentinel.LogViewer-{version}-win-x64.msi",
		$"Sentinel.LogViewer-SelfContained-win-x64-{version}.zip",
		$"Sentinel.LogViewer-FrameworkDependent-win-x64-{version}.zip"
	};

	[Theory]
	[InlineData("1.0.0-alpha", "1.0.0-alpha.1")]
	[InlineData("1.0.0-alpha.1", "1.0.0-alpha.beta")]
	[InlineData("1.0.0-alpha.beta", "1.0.0-beta")]
	[InlineData("1.0.0-beta.2", "1.0.0-beta.11")]
	[InlineData("1.0.0-rc.1", "1.0.0")]
	[InlineData("4.0.0", "5.0.0-beta.1")]
	[InlineData("5.0.0-beta.1", "5.0.0")]
	[InlineData("5.0.0", "5.0.1")]
	[InlineData("5.9.0", "5.10.0")]
	public void CompareTo_FollowsSemVerPrecedence(string lower, string higher)
	{
		Assert.True(Version(lower).CompareTo(Version(higher)) < 0);
		Assert.True(Version(higher).CompareTo(Version(lower)) > 0);
	}

	[Theory]
	[InlineData("v5.0.0-beta.1", "5.0.0-beta.1", true)]
	[InlineData("5.0.0-beta.1+623325c923ce", "5.0.0-beta.1", true)]
	[InlineData("V4.0.0", "4.0.0", false)]
	public void TryParse_AcceptsTagsAndInformationalVersions(string text, string expected, bool isPrerelease)
	{
		var version = Version(text);
		Assert.Equal(expected, version.ToString());
		Assert.Equal(isPrerelease, version.IsPrerelease);
	}

	[Theory]
	[InlineData("5.0.0-beta.3", "5.0.0-beta3")]
	[InlineData("5.0.0-rc.12", "5.0.0-rc12")]
	[InlineData("5.0.0-alpha.1", "5.0.0-alpha1")]
	[InlineData("5.0.0", "5.0.0")]
	public void ChocolateyVersion_IsWrittenLikeTheNameOfThePackage(string version, string expected)
	{
		Assert.Equal(expected, Version(version).ChocolateyVersion);
	}

	[Theory]
	[InlineData("5.0.0-beta.3", "choco upgrade Sentinel.LogViewer --pre -y --version 5.0.0-beta3")]
	[InlineData("5.1.0", "choco upgrade Sentinel.LogViewer -y --version 5.1.0")]
	public void TheChocolateyCommand_NamesTheVersion(string version, string expected)
	{
		Assert.Equal(expected, AboutViewModel.ChocolateyCommandFor(Version(version)));
	}

	[Fact]
	public void TheChocolateyCommand_WithoutAKnownVersion_UpgradesToTheNewestOne()
	{
		Assert.Equal("choco upgrade Sentinel.LogViewer -y", AboutViewModel.ChocolateyCommandFor(null));
	}

	[Theory]
	[InlineData("dev-3.2.0-dev.4.1")]
	[InlineData("5.0")]
	[InlineData("")]
	public void TryParse_RejectsNonVersions(string text)
	{
		Assert.False(ReleaseVersion.TryParse(text, out _));
	}

	private static FeedRelease Feed(string tag) => new(tag, Version(tag.TrimStart('v')), $"https://github.com/boexler/NLogViewer/releases/tag/{tag}");

	private const string AtomFeed = """
		<?xml version="1.0" encoding="UTF-8"?>
		<feed xmlns="http://www.w3.org/2005/Atom">
		  <title>Release notes from NLogViewer</title>
		  <entry><title>Sentinel.LogViewer 99.0.0</title><link rel="alternate" type="text/html" href="https://github.com/boexler/NLogViewer/releases/tag/v99.0.0"/></entry>
		  <entry><title>Sentinel.LogViewer 5.0.0-beta.4</title><link rel="alternate" type="text/html" href="https://github.com/boexler/NLogViewer/releases/tag/v5.0.0-beta.4"/></entry>
		  <entry><title>Something else</title><link rel="alternate" type="text/html" href="https://github.com/boexler/NLogViewer/releases/tag/nightly"/></entry>
		  <entry><title>Without a link</title></entry>
		</feed>
		""";

	[Fact]
	public void ParseFeed_ReadsTheTagsOfTheEntries_AndSkipsWhatIsNoVersion()
	{
		var releases = UpdateService.ParseFeed(AtomFeed);

		Assert.Equal(new[] { "v99.0.0", "v5.0.0-beta.4" }, releases.Select(release => release.Tag));
		Assert.Equal("https://github.com/boexler/NLogViewer/releases/tag/v99.0.0", releases[0].Url);
		Assert.True(releases[1].Version.IsPrerelease);
	}

	[Fact]
	public void SelectUpdate_ReturnsTheNewestStableRelease_UnlessPrereleasesAreWanted()
	{
		var releases = new[] { Feed("v5.1.0-beta.1"), Feed("v5.0.0"), Feed("v4.0.0") };

		var stable = UpdateService.SelectUpdate(releases, Version("4.0.0"), includePrereleases: false);
		var withPrereleases = UpdateService.SelectUpdate(releases, Version("4.0.0"), includePrereleases: true);

		Assert.Equal(("5.0.0", "v5.0.0"), (stable?.Version.ToString(), stable?.Tag));
		Assert.Null(stable?.Asset);
		Assert.Equal("5.1.0-beta.1", withPrereleases?.Version.ToString());
	}

	[Fact]
	public void SelectUpdate_IgnoresOlderAndEqualVersions() =>
		Assert.Null(UpdateService.SelectUpdate(new[] { Feed("v5.0.0"), Feed("v4.0.0") }, Version("5.0.0"), includePrereleases: true));

	[Theory]
	[InlineData("5.0.0-beta.5", "5.0.0-beta.5", true)]
	[InlineData("5.0.0-beta.5", "v5.0.0", true)]
	[InlineData("5.0.0", "5.0.0-beta.5", false)]
	[InlineData("5.0.1", "5.0.0", false)]
	[InlineData("5.0.0", "", false)]
	[InlineData("5.0.0", "no version", false)]
	public void IsSkipped_IsTrueForTheSkippedVersionAndOlderOnes_AndFalseForNewerOnes(string offered, string skipped, bool expected)
	{
		var update = UpdateService.SelectUpdate(new[] { Feed("v" + offered) }, Version("1.0.0"), includePrereleases: true)!;

		Assert.Equal(expected, UpdateService.IsSkipped(update, skipped));
	}

	private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
	{
		public List<string> Requests { get; } = new();

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Requests.Add(request.RequestUri!.ToString());
			return Task.FromResult(respond(request));
		}
	}

	private static HttpResponseMessage Text(string content) => new(HttpStatusCode.OK) { Content = new StringContent(content) };

	[Fact]
	public async Task CheckForUpdateAsync_AsksTheFeedAndNotTheApi_AndNotAgainWithinTenMinutes()
	{
		var handler = new StubHandler(_ => Text(AtomFeed));
		var service = new UpdateService(handler, InstallType.Msi);

		var first = await service.CheckForUpdateAsync(includePrereleases: false);
		var second = await service.CheckForUpdateAsync(includePrereleases: true);

		Assert.Equal("99.0.0", first?.Version.ToString());
		Assert.Equal("99.0.0", second?.Version.ToString());
		Assert.Equal(new[] { "https://github.com/boexler/NLogViewer/releases.atom" }, handler.Requests);
	}

	private static string ReleaseJson() => JsonSerializer.Serialize(new GitHubRelease
	{
		TagName = "v99.0.0",
		Assets = AllAssets("99.0.0").Select(name => new GitHubAsset { Name = name, BrowserDownloadUrl = $"https://example.invalid/{name}", Size = 42, Digest = "sha256:ABC123" }).ToList()
	});

	private static AvailableUpdate Update() => new(Version("99.0.0"), "https://github.com/boexler/NLogViewer/releases/tag/v99.0.0", "v99.0.0", null);

	[Theory]
	[InlineData(InstallType.Msi, false, "Sentinel.LogViewer-99.0.0-win-x64.msi")]
	[InlineData(InstallType.Portable, true, "Sentinel.LogViewer-FrameworkDependent-win-x64-99.0.0.zip")]
	[InlineData(InstallType.Portable, false, "Sentinel.LogViewer-SelfContained-win-x64-99.0.0.zip")]
	public async Task ResolveAssetAsync_PicksTheFileOfTheInstallation_FromTheApi(InstallType installType, bool frameworkDependent, string expected)
	{
		var handler = new StubHandler(_ => Text(ReleaseJson()));
		var service = new UpdateService(handler, installType);

		var asset = await service.ResolveAssetAsync(Update(), frameworkDependent, CancellationToken.None);

		Assert.Equal((expected, "ABC123"), (asset.Name, asset.Sha256));
		Assert.Equal(new[] { "https://api.github.com/repos/boexler/NLogViewer/releases/tags/v99.0.0" }, handler.Requests);
	}

	[Fact]
	public async Task ResolveAssetAsync_WithoutAFile_SaysThatTheFilesAreMissing()
	{
		var service = new UpdateService(new StubHandler(_ => Text(JsonSerializer.Serialize(new GitHubRelease { TagName = "v99.0.0" }))), InstallType.Msi);

		await Assert.ThrowsAsync<ReleaseFilesMissingException>(() => service.ResolveAssetAsync(Update(), false, CancellationToken.None));
	}

	[Fact]
	public async Task TheApiLimit_IsReportedWithTheTimeWhenRequestsAreAllowedAgain()
	{
		var service = new UpdateService(new StubHandler(_ =>
		{
			var response = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("rate limit exceeded") };
			response.Headers.Add("X-RateLimit-Remaining", "0");
			response.Headers.Add("X-RateLimit-Reset", "1900000000");
			return response;
		}), InstallType.Msi);

		var exception = await Assert.ThrowsAsync<GitHubRateLimitException>(() => service.ResolveAssetAsync(Update(), false, CancellationToken.None));
		await Assert.ThrowsAsync<GitHubRateLimitException>(() => service.GetReleasesAsync());

		Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1900000000), exception.ResetAt);
	}

	[Fact]
	public async Task AForbiddenAnswerWithoutTheLimit_IsAnOrdinaryError()
	{
		var service = new UpdateService(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)), InstallType.Msi);

		await Assert.ThrowsAsync<HttpRequestException>(() => service.ResolveAssetAsync(Update(), false, CancellationToken.None));
	}

	[Fact]
	public void Detect_PortableMarker_IsPortable()
	{
		using var directory = new TemporaryDirectory();
		File.WriteAllText(Path.Combine(directory.Path, ConfigurationService.PortableMarkerFileName), string.Empty);

		Assert.Equal(InstallType.Portable, InstallTypeDetector.Detect(directory.Path, null, @"C:\Program Files"));
	}

	[Fact]
	public void Detect_InstallFolderWithChocolateyPackage_IsChocolatey()
	{
		using var programFiles = new TemporaryDirectory();
		using var chocolatey = new TemporaryDirectory();
		var installFolder = Directory.CreateDirectory(Path.Combine(programFiles.Path, "Boexler", "Sentinel.LogViewer")).FullName;
		Directory.CreateDirectory(Path.Combine(chocolatey.Path, "lib", InstallTypeDetector.ChocolateyPackageId));

		Assert.Equal(InstallType.Chocolatey, InstallTypeDetector.Detect(installFolder, chocolatey.Path, programFiles.Path));
		Assert.Equal(InstallType.Msi, InstallTypeDetector.Detect(installFolder, null, programFiles.Path));
	}

	[Fact]
	public void Detect_BuildOutputFolder_IsDevelopment()
	{
		using var directory = new TemporaryDirectory();

		Assert.Equal(InstallType.Development, InstallTypeDetector.Detect(directory.Path, null, @"C:\Program Files"));
	}

	private sealed class TemporaryDirectory : IDisposable
	{
		public string Path { get; } = Directory.CreateDirectory(
			System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nlv_update_" + Guid.NewGuid().ToString("N"))).FullName;

		public void Dispose() => Directory.Delete(Path, recursive: true);
	}
}
