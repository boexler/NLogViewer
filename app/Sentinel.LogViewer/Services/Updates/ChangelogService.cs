using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Sentinel.LogViewer.Services.Updates;

/// <summary>
/// The release notes of one version
/// </summary>
/// <param name="Markdown">The notes as Markdown, without the installation hints at the top of a release</param>
public sealed record ChangelogEntry(ReleaseVersion Version, DateTimeOffset? PublishedAt, bool IsPrerelease, string Markdown, string Url);

/// <summary>
/// The versions of the changelog, newest first
/// </summary>
/// <param name="IsFromCache">Whether GitHub could not be reached and the list that was saved earlier is shown</param>
/// <param name="SavedAt">When the saved list was loaded from GitHub; null when there is none</param>
public sealed record ChangelogList(IReadOnlyList<ChangelogEntry> Entries, bool IsFromCache, DateTimeOffset? SavedAt)
{
	public static ChangelogList Empty { get; } = new(Array.Empty<ChangelogEntry>(), true, null);
}

/// <summary>
/// Loads the release notes of all versions from the GitHub releases and keeps the list on disk for the times without a network
/// </summary>
public class ChangelogService
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	private readonly UpdateService _updateService;
	private readonly string _cachePath;

	public ChangelogService(UpdateService updateService, ConfigurationService configurationService)
		: this(updateService, Path.Combine(configurationService.DataDirectory, "changelog.json"))
	{
	}

	/// <summary>
	/// For tests: the list is kept in <paramref name="cachePath"/>
	/// </summary>
	internal ChangelogService(UpdateService updateService, string cachePath)
	{
		_updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
		_cachePath = cachePath;
	}

	/// <summary>
	/// Loads the releases from GitHub and saves them; when GitHub cannot be reached the saved list is returned
	/// </summary>
	public async Task<ChangelogList> LoadAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			var releases = await _updateService.GetReleasesAsync(cancellationToken).ConfigureAwait(false);
			var entries = Parse(releases);
			Save(releases);
			return new ChangelogList(entries, false, DateTimeOffset.Now);
		}
		catch (Exception ex) when (!cancellationToken.IsCancellationRequested &&
		                           ex is HttpRequestException or TaskCanceledException or JsonException or IOException or GitHubRateLimitException)
		{
			return LoadSaved();
		}
	}

	/// <summary>
	/// Returns the list that was saved by the last successful <see cref="LoadAsync"/>, or an empty list
	/// </summary>
	public ChangelogList LoadSaved()
	{
		try
		{
			if (!File.Exists(_cachePath))
				return ChangelogList.Empty;

			var saved = JsonSerializer.Deserialize<SavedChangelog>(File.ReadAllText(_cachePath));
			return saved == null ? ChangelogList.Empty : new ChangelogList(Parse(saved.Releases), true, saved.SavedAt);
		}
		catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
		{
			return ChangelogList.Empty;
		}
	}

	/// <summary>
	/// Turns releases into entries: releases that are drafts or have no version (the development builds) are left out, newest first
	/// </summary>
	public static IReadOnlyList<ChangelogEntry> Parse(IEnumerable<GitHubRelease> releases)
	{
		var entries = new List<ChangelogEntry>();
		foreach (var release in releases)
		{
			if (release.Draft || !ReleaseVersion.TryParse(release.TagName, out var version))
				continue;

			entries.Add(new ChangelogEntry(version, release.PublishedAt, release.Prerelease || version.IsPrerelease,
				StripInstallationNotes(release.Body ?? string.Empty), release.HtmlUrl));
		}

		return entries
			.GroupBy(entry => entry.Version.ToString())
			.Select(group => group.First())
			.OrderByDescending(entry => entry.Version)
			.ToList();
	}

	/// <summary>
	/// The versions that came after <paramref name="previous"/> up to and including <paramref name="current"/>, newest first
	/// </summary>
	public static IReadOnlyList<ChangelogEntry> VersionsBetween(IEnumerable<ChangelogEntry> entries, ReleaseVersion previous, ReleaseVersion current) =>
		entries.Where(entry => entry.Version.CompareTo(previous) > 0 && entry.Version.CompareTo(current) <= 0)
			.OrderByDescending(entry => entry.Version)
			.ToList();

	/// <summary>
	/// Removes the block at the top of a release that tells how to install it (Chocolatey, NuGet): the application is installed already
	/// </summary>
	public static string StripInstallationNotes(string markdown)
	{
		var lines = markdown.Replace("\r\n", "\n").Split('\n').ToList();
		var end = 0;
		while (end < lines.Count && lines[end].TrimStart().StartsWith('>'))
			end++;

		if (end == 0 || !lines.Take(end).Any(line => Regex.IsMatch(line, "Chocolatey|NuGet", RegexOptions.IgnoreCase)))
			return markdown.Trim();

		return string.Join("\n", lines.Skip(end)).Trim();
	}

	private void Save(IEnumerable<GitHubRelease> releases)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
			var saved = new SavedChangelog
			{
				SavedAt = DateTimeOffset.Now,
				Releases = releases.Select(release => new GitHubRelease
				{
					TagName = release.TagName,
					HtmlUrl = release.HtmlUrl,
					Draft = release.Draft,
					Prerelease = release.Prerelease,
					PublishedAt = release.PublishedAt,
					Body = release.Body
				}).ToList()
			};
			File.WriteAllText(_cachePath, JsonSerializer.Serialize(saved, JsonOptions));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// The changelog also works without the saved list.
		}
	}

	private sealed class SavedChangelog
	{
		public DateTimeOffset SavedAt { get; set; }
		public List<GitHubRelease> Releases { get; set; } = new();
	}
}
