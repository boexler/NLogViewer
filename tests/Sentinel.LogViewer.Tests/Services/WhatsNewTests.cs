using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Updates;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// Tests for <see cref="WhatsNew"/>: the changelog opens once after an update to a release version
/// </summary>
public class WhatsNewTests
{
	private static ReleaseVersion Version(string text)
	{
		Assert.True(ReleaseVersion.TryParse(text, out var version));
		return version;
	}

	[Theory]
	[InlineData("5.0.0", true)]
	[InlineData("5.0.0-beta.2", true)]
	[InlineData("5.1.0-rc.1", true)]
	[InlineData("5.0.0-alpha.10", true)]
	[InlineData("5.0.0-190-tab-header-count-after-clear.24", false)]
	[InlineData("5.0.0-beta", false)]
	[InlineData("3.2.0-dev.4.1", false)]
	public void IsReleaseVersion_IsTrueForReleasesAndFalseForBuildsOfABranch(string version, bool expected)
	{
		Assert.Equal(expected, WhatsNew.IsReleaseVersion(Version(version)));
	}

	[Fact]
	public void AfterAnUpdate_ThePreviousVersionIsReturned()
	{
		var configuration = new AppConfiguration { LastSeenVersion = "5.0.0-beta.2" };

		var previous = WhatsNew.PreviousVersionToShow(configuration, Version("5.0.0-beta.3"));

		Assert.Equal("5.0.0-beta.2", previous?.ToString());
	}

	[Theory]
	[InlineData("", "5.0.0-beta.3")]
	[InlineData("not a version", "5.0.0-beta.3")]
	[InlineData("5.0.0-beta.3", "5.0.0-beta.3")]
	[InlineData("5.0.0-beta.4", "5.0.0-beta.3")]
	[InlineData("5.0.0-beta.2", "5.0.0-190-a-branch.24")]
	public void ThereIsNothingToShow_AtTheFirstStart_AfterADowngrade_Or_WithoutAnUpdate_Or_ForABuildOfABranch(string lastSeen, string current)
	{
		var configuration = new AppConfiguration { LastSeenVersion = lastSeen };

		Assert.Null(WhatsNew.PreviousVersionToShow(configuration, Version(current)));
	}

	[Fact]
	public void WhenTheSettingIsOff_NothingIsShown()
	{
		var configuration = new AppConfiguration { LastSeenVersion = "5.0.0-beta.2", ShowChangelogAfterUpdate = false };

		Assert.Null(WhatsNew.PreviousVersionToShow(configuration, Version("5.0.0-beta.3")));
	}

	[Fact]
	public void TheSettingIsOn_ByDefault()
	{
		Assert.True(new AppConfiguration().ShowChangelogAfterUpdate);
		Assert.True(ConfigurationService.CreateDefaultConfiguration().ShowChangelogAfterUpdate);
	}
}
