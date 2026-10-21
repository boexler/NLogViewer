using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sentinel.LogViewer.Services.Updates;

/// <summary>A downloadable file of a release.</summary>
/// <param name="Sha256">Hex SHA-256 of the file when GitHub provides it, otherwise null</param>
public sealed record UpdateAsset(string Name, string DownloadUrl, long Size, string? Sha256);

/// <summary>A release that is newer than the running version.</summary>
/// <param name="Tag">The tag of the release, e.g. <c>v5.0.0-beta.5</c></param>
/// <param name="Asset">The file to install; null until <see cref="UpdateService.ResolveAssetAsync(AvailableUpdate, CancellationToken)"/> looked it up, and for install types that do not install updates themselves</param>
public sealed record AvailableUpdate(ReleaseVersion Version, string ReleasePageUrl, string Tag, UpdateAsset? Asset);

/// <summary>A release in the Atom feed of the releases</summary>
public sealed record FeedRelease(string Tag, ReleaseVersion Version, string Url);

/// <summary>
/// Checks GitHub Releases for a newer version, downloads it and hands the installation over to a helper process. The check reads the Atom
/// feed of the releases, which has no limit of requests; the API, which allows only 60 requests an hour without signing in, is asked only
/// for the files of a release and for the changelog.
/// </summary>
public class UpdateService
{
	/// <summary>GitHub repository (owner/name) that publishes the releases.</summary>
	public const string Repository = "boexler/NLogViewer";

	public const string RepositoryUrl = "https://github.com/" + Repository;

	/// <summary>shortcut: the feed is kept in memory only, and not asked again for ten minutes; keep it on disk with its ETag if the check is made more often</summary>
	private static readonly TimeSpan FeedLifetime = TimeSpan.FromMinutes(10);

	private readonly HttpClient _httpClient;
	private List<FeedRelease>? _feed;
	private DateTime _feedReadAt;

	public UpdateService() : this(new HttpClientHandler())
	{
	}

	/// <summary>For tests: the requests go through <paramref name="handler"/></summary>
	internal UpdateService(HttpMessageHandler handler, InstallType? installType = null)
	{
		InstallType = installType ?? InstallTypeDetector.Detect();
		_httpClient = new HttpClient(handler);
		// GitHub rejects API requests without a user agent.
		_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"{ProductNames.Technical}/{CurrentVersion}");
		_httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
	}

	/// <summary>The version of the running application.</summary>
	public static ReleaseVersion CurrentVersion { get; } = ReadCurrentVersion();

	public InstallType InstallType { get; }

	/// <summary>Whether this copy downloads and installs updates itself (MSI and portable) instead of pointing to the release or to Chocolatey</summary>
	public bool CanInstallItself => InstallType is InstallType.Portable or InstallType.Msi;

	/// <summary>
	/// Whether prerelease versions are offered; unless set explicitly, as long as a prerelease is running
	/// </summary>
	public static bool IncludesPrereleases(AppConfiguration configuration) =>
		configuration.IncludePrereleaseUpdates ?? CurrentVersion.IsPrerelease;

	/// <summary>
	/// Returns the newest release that is newer than the running version, or null when the application is up to date. It asks the feed of the
	/// releases, not the API; the file to install is looked up later with <see cref="ResolveAssetAsync(AvailableUpdate, CancellationToken)"/>.
	/// </summary>
	/// <param name="includePrereleases">Whether prerelease versions are offered</param>
	public async Task<AvailableUpdate?> CheckForUpdateAsync(bool includePrereleases, CancellationToken cancellationToken = default) =>
		SelectUpdate(await GetFeedAsync(cancellationToken).ConfigureAwait(false), CurrentVersion, includePrereleases);

	/// <summary>
	/// Whether the user skipped this version, or a newer one; the next release after the skipped one is announced again
	/// </summary>
	public static bool IsSkipped(AvailableUpdate update, string? skippedVersion) =>
		ReleaseVersion.TryParse(skippedVersion, out var skipped) && update.Version.CompareTo(skipped) <= 0;

	private async Task<List<FeedRelease>> GetFeedAsync(CancellationToken cancellationToken)
	{
		if (_feed != null && DateTime.UtcNow - _feedReadAt < FeedLifetime)
			return _feed;

		using var request = new HttpRequestMessage(HttpMethod.Get, $"{RepositoryUrl}/releases.atom");
		request.Headers.Accept.ParseAdd("application/atom+xml");
		using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		_feed = ParseFeed(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
		_feedReadAt = DateTime.UtcNow;
		return _feed;
	}

	/// <summary>
	/// Reads the releases of an Atom feed; an entry whose tag is not a version is left out
	/// </summary>
	public static List<FeedRelease> ParseFeed(string xml)
	{
		const string marker = "/releases/tag/";
		XNamespace atom = "http://www.w3.org/2005/Atom";
		var releases = new List<FeedRelease>();
		foreach (var entry in XDocument.Parse(xml).Descendants(atom + "entry"))
		{
			var href = entry.Elements(atom + "link").FirstOrDefault(link => (string?)link.Attribute("rel") == "alternate")?.Attribute("href")?.Value;
			var at = href?.IndexOf(marker, StringComparison.Ordinal) ?? -1;
			if (href == null || at < 0)
				continue;

			var tag = Uri.UnescapeDataString(href[(at + marker.Length)..]);
			if (ReleaseVersion.TryParse(tag, out var version))
				releases.Add(new FeedRelease(tag, version, href));
		}

		return releases;
	}

	/// <summary>
	/// Selects the newest release that is newer than <paramref name="currentVersion"/>
	/// </summary>
	public static AvailableUpdate? SelectUpdate(IEnumerable<FeedRelease> releases, ReleaseVersion currentVersion, bool includePrereleases)
	{
		AvailableUpdate? best = null;
		foreach (var release in releases)
		{
			if (!includePrereleases && release.Version.IsPrerelease)
				continue;

			if (release.Version.CompareTo(currentVersion) <= 0 || (best != null && release.Version.CompareTo(best.Version) <= 0))
				continue;

			best = new AvailableUpdate(release.Version, release.Url, release.Tag, null);
		}

		return best;
	}

	/// <summary>
	/// Looks up the file of the release that this installation installs. This asks the API, so it is done when the user wants to download.
	/// </summary>
	/// <exception cref="GitHubRateLimitException">The limit of requests per hour is reached</exception>
	/// <exception cref="ReleaseFilesMissingException">The release has no such file, e.g. because it is still being uploaded</exception>
	public Task<UpdateAsset> ResolveAssetAsync(AvailableUpdate update, CancellationToken cancellationToken = default) =>
		ResolveAssetAsync(update, InstallTypeDetector.IsFrameworkDependent(AppContext.BaseDirectory), cancellationToken);

	internal async Task<UpdateAsset> ResolveAssetAsync(AvailableUpdate update, bool frameworkDependent, CancellationToken cancellationToken)
	{
		var release = await GetApiAsync<GitHubRelease>(
			$"https://api.github.com/repos/{Repository}/releases/tags/{Uri.EscapeDataString(update.Tag)}", cancellationToken).ConfigureAwait(false);
		var file = release?.Assets.FirstOrDefault(a => IsAssetFor(a.Name, InstallType, frameworkDependent))
		           ?? throw new ReleaseFilesMissingException(update.Tag);
		return new UpdateAsset(file.Name, file.BrowserDownloadUrl, file.Size, ParseSha256(file.Digest));
	}

	/// <summary>
	/// Asks the API; when its limit of requests is used up this throws <see cref="GitHubRateLimitException"/> instead of a bare status code
	/// </summary>
	private async Task<T?> GetApiAsync<T>(string url, CancellationToken cancellationToken)
	{
		using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
		if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
		    && response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.FirstOrDefault() == "0")
		{
			DateTimeOffset? resetAt = response.Headers.TryGetValues("X-RateLimit-Reset", out var reset) && long.TryParse(reset.FirstOrDefault(), out var seconds)
				? DateTimeOffset.FromUnixTimeSeconds(seconds)
				: null;
			throw new GitHubRateLimitException(resetAt);
		}

		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Returns all releases of the repository, with their notes, page by page
	/// </summary>
	public virtual async Task<List<GitHubRelease>> GetReleasesAsync(CancellationToken cancellationToken = default)
	{
		const int pageSize = 100;
		const int maxPages = 5;
		var releases = new List<GitHubRelease>();
		for (var page = 1; page <= maxPages; page++)
		{
			var pageOfReleases = await GetApiAsync<List<GitHubRelease>>(
				$"https://api.github.com/repos/{Repository}/releases?per_page={pageSize}&page={page}", cancellationToken).ConfigureAwait(false);
			if (pageOfReleases == null || pageOfReleases.Count == 0)
				break;

			releases.AddRange(pageOfReleases);
			if (pageOfReleases.Count < pageSize)
				break;
		}

		return releases;
	}

	/// <summary>
	/// Returns whether a release file is the one to install for the given install type
	/// </summary>
	public static bool IsAssetFor(string name, InstallType installType, bool frameworkDependent)
	{
		return installType switch
		{
			InstallType.Msi => name.EndsWith("-win-x64.msi", StringComparison.OrdinalIgnoreCase),
			InstallType.Portable => name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
			                        name.Contains(frameworkDependent ? "-FrameworkDependent-win-x64-" : "-SelfContained-win-x64-",
				                        StringComparison.OrdinalIgnoreCase),
			_ => false
		};
	}

	/// <summary>
	/// Downloads the file of an update into the temp folder and verifies its checksum when one is known
	/// </summary>
	/// <param name="progress">Receives the downloaded and the total number of bytes</param>
	/// <returns>Path of the downloaded file</returns>
	public async Task<string> DownloadAsync(AvailableUpdate update, IProgress<(long Downloaded, long Total)>? progress,
		CancellationToken cancellationToken = default)
	{
		var asset = update.Asset ?? throw new InvalidOperationException("This update has no file to download.");
		var directory = Path.Combine(Path.GetTempPath(), ProductNames.Technical, "updates", update.Version.ToString());
		Directory.CreateDirectory(directory);
		var filePath = Path.Combine(directory, asset.Name);

		using (var response = await _httpClient.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
			       .ConfigureAwait(false))
		{
			response.EnsureSuccessStatusCode();
			var total = response.Content.Headers.ContentLength ?? asset.Size;

			await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
			await using var target = File.Create(filePath);
			var buffer = new byte[81920];
			long downloaded = 0;
			int read;
			while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
			{
				await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
				downloaded += read;
				progress?.Report((downloaded, total));
			}
		}

		if (asset.Sha256 != null)
		{
			await using var file = File.OpenRead(filePath);
			var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false));
			if (!hash.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
			{
				file.Close();
				File.Delete(filePath);
				throw new InvalidDataException($"The downloaded file {asset.Name} does not match its checksum.");
			}
		}

		return filePath;
	}

	/// <summary>
	/// Starts the helper that installs the downloaded update once this process has exited and then starts the new version.
	/// The caller shuts the application down afterwards.
	/// </summary>
	public void StartInstallation(string downloadedFile)
	{
		var scriptPath = Path.Combine(Path.GetDirectoryName(downloadedFile)!, "install-update.ps1");
		File.WriteAllText(scriptPath, UpdateHelperScript.Content);

		var startInfo = new ProcessStartInfo("powershell.exe")
		{
			UseShellExecute = false,
			CreateNoWindow = true
		};
		foreach (var argument in new[]
		         {
			         "-NoProfile", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-File", scriptPath,
			         "-ProcessId", Environment.ProcessId.ToString(),
			         "-Package", downloadedFile,
			         // A trailing backslash would escape the closing quote when the path is passed on.
			         "-ApplicationDirectory", Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory),
			         "-Executable", Environment.ProcessPath ?? string.Empty
		         })
		{
			startInfo.ArgumentList.Add(argument);
		}

		Process.Start(startInfo);
	}

	private static string? ParseSha256(string? digest)
	{
		const string prefix = "sha256:";
		return digest != null && digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? digest[prefix.Length..] : null;
	}

	private static ReleaseVersion ReadCurrentVersion()
	{
		var assembly = typeof(UpdateService).Assembly;
		var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		if (ReleaseVersion.TryParse(informationalVersion, out var version))
			return version;

		var assemblyVersion = assembly.GetName().Version ?? new Version(0, 0, 0);
		ReleaseVersion.TryParse($"{assemblyVersion.Major}.{assemblyVersion.Minor}.{Math.Max(0, assemblyVersion.Build)}", out version);
		return version;
	}
}

/// <summary>A release as returned by the GitHub REST API.</summary>
public sealed class GitHubRelease
{
	[JsonPropertyName("tag_name")] public string TagName { get; set; } = string.Empty;
	[JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = string.Empty;
	[JsonPropertyName("body")] public string? Body { get; set; }
	[JsonPropertyName("published_at")] public DateTimeOffset? PublishedAt { get; set; }
	[JsonPropertyName("draft")] public bool Draft { get; set; }
	[JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
	[JsonPropertyName("assets")] public List<GitHubAsset> Assets { get; set; } = new();
}

/// <summary>A release file as returned by the GitHub REST API.</summary>
public sealed class GitHubAsset
{
	[JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
	[JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = string.Empty;
	[JsonPropertyName("size")] public long Size { get; set; }
	[JsonPropertyName("digest")] public string? Digest { get; set; }
}
