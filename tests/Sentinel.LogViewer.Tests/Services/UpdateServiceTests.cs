using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Updates;
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
	[InlineData("dev-3.2.0-dev.4.1")]
	[InlineData("5.0")]
	[InlineData("")]
	public void TryParse_RejectsNonVersions(string text)
	{
		Assert.False(ReleaseVersion.TryParse(text, out _));
	}

	[Fact]
	public void SelectUpdate_ReturnsNewestStableReleaseWithItsFile()
	{
		var releases = new[]
		{
			Release("v5.1.0-beta.1", prerelease: true, assets: AllAssets("5.1.0-beta.1")),
			Release("v5.0.0", assets: AllAssets("5.0.0")),
			Release("v4.0.0", assets: AllAssets("4.0.0"))
		};

		var update = UpdateService.SelectUpdate(releases, Version("4.0.0"), includePrereleases: false, InstallType.Msi, frameworkDependent: false);

		Assert.NotNull(update);
		Assert.Equal("5.0.0", update.Version.ToString());
		Assert.Equal("Sentinel.LogViewer-5.0.0-win-x64.msi", update.Asset?.Name);
		Assert.Equal("ABC123", update.Asset?.Sha256);
	}

	[Fact]
	public void SelectUpdate_WithPrereleases_ReturnsNewestPrerelease()
	{
		var releases = new[]
		{
			Release("v5.0.0-beta.2", prerelease: true, assets: AllAssets("5.0.0-beta.2")),
			Release("v5.0.0-beta.1", prerelease: true, assets: AllAssets("5.0.0-beta.1"))
		};

		var update = UpdateService.SelectUpdate(releases, Version("5.0.0-beta.1"), includePrereleases: true, InstallType.Portable, frameworkDependent: true);

		Assert.Equal("5.0.0-beta.2", update?.Version.ToString());
		Assert.Equal("Sentinel.LogViewer-FrameworkDependent-win-x64-5.0.0-beta.2.zip", update?.Asset?.Name);
	}

	[Fact]
	public void SelectUpdate_IgnoresDraftsOlderVersionsAndReleasesWithoutFile()
	{
		var releases = new[]
		{
			Release("v6.0.0", draft: true, assets: AllAssets("6.0.0")),
			// Release workflow still running: no files attached yet.
			Release("v5.1.0"),
			Release("v4.0.0", assets: AllAssets("4.0.0"))
		};

		Assert.Null(UpdateService.SelectUpdate(releases, Version("5.0.0"), includePrereleases: true, InstallType.Msi, frameworkDependent: false));
	}

	[Theory]
	[InlineData(InstallType.Chocolatey)]
	[InlineData(InstallType.Development)]
	public void SelectUpdate_InstallTypeWithoutSelfUpdate_ReturnsUpdateWithoutFile(InstallType installType)
	{
		var update = UpdateService.SelectUpdate(new[] { Release("v5.0.0") }, Version("4.0.0"), includePrereleases: false, installType, frameworkDependent: false);

		Assert.Equal("5.0.0", update?.Version.ToString());
		Assert.Null(update?.Asset);
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
