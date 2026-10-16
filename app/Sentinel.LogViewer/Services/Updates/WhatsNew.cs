using System.Text.RegularExpressions;

namespace Sentinel.LogViewer.Services.Updates;

/// <summary>
/// Decides whether the changelog opens at the start: once after an update to a release version
/// </summary>
public static class WhatsNew
{
	private static readonly Regex ReleaseLabel = new(@"^(?:alpha|beta|rc|preview)\.\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

	/// <summary>
	/// Whether the version is one that was released: a stable version or a prerelease like <c>5.0.0-beta.2</c>, not a build of a branch
	/// </summary>
	public static bool IsReleaseVersion(ReleaseVersion version) => !version.IsPrerelease || ReleaseLabel.IsMatch(version.Prerelease);

	/// <summary>
	/// Returns the version that ran before when the changelog is to open now, otherwise null: the setting is off, this is the first start,
	/// the version is the same or lower, or this is not a release version
	/// </summary>
	public static ReleaseVersion? PreviousVersionToShow(AppConfiguration configuration, ReleaseVersion current)
	{
		if (!configuration.ShowChangelogAfterUpdate || !IsReleaseVersion(current))
			return null;

		return ReleaseVersion.TryParse(configuration.LastSeenVersion, out var last) && last.CompareTo(current) < 0 ? last : null;
	}
}
