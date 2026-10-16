using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Sentinel.LogViewer.Services.Updates;

/// <summary>A downloadable file of a release.</summary>
/// <param name="Sha256">Hex SHA-256 of the file when GitHub provides it, otherwise null</param>
public sealed record UpdateAsset(string Name, string DownloadUrl, long Size, string? Sha256);

/// <summary>A release that is newer than the running version.</summary>
/// <param name="Asset">The file to install, or null when the install type does not install updates itself</param>
public sealed record AvailableUpdate(ReleaseVersion Version, string ReleasePageUrl, UpdateAsset? Asset);

/// <summary>
/// Checks GitHub Releases for a newer version, downloads it and hands the installation over to a helper process
/// </summary>
public class UpdateService
{
	/// <summary>GitHub repository (owner/name) that publishes the releases.</summary>
	public const string Repository = "boexler/NLogViewer";

	public const string RepositoryUrl = "https://github.com/" + Repository;

	private readonly HttpClient _httpClient;

	public UpdateService()
	{
		_httpClient = new HttpClient();
		// GitHub rejects API requests without a user agent.
		_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"{ProductNames.Technical}/{CurrentVersion}");
		_httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
	}

	/// <summary>The version of the running application.</summary>
	public static ReleaseVersion CurrentVersion { get; } = ReadCurrentVersion();

	public InstallType InstallType { get; } = InstallTypeDetector.Detect();

	/// <summary>
	/// Whether prerelease versions are offered; unless set explicitly, as long as a prerelease is running
	/// </summary>
	public static bool IncludesPrereleases(AppConfiguration configuration) =>
		configuration.IncludePrereleaseUpdates ?? CurrentVersion.IsPrerelease;

	/// <summary>
	/// Returns the newest release that is newer than the running version, or null when the application is up to date
	/// </summary>
	/// <param name="includePrereleases">Whether prerelease versions are offered</param>
	public async Task<AvailableUpdate?> CheckForUpdateAsync(bool includePrereleases, CancellationToken cancellationToken = default)
	{
		var releases = await _httpClient.GetFromJsonAsync<List<GitHubRelease>>(
			$"https://api.github.com/repos/{Repository}/releases?per_page=30", cancellationToken).ConfigureAwait(false);

		return SelectUpdate(releases ?? new List<GitHubRelease>(), CurrentVersion, includePrereleases,
			InstallType, InstallTypeDetector.IsFrameworkDependent(AppContext.BaseDirectory));
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
			var pageOfReleases = await _httpClient.GetFromJsonAsync<List<GitHubRelease>>(
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
	/// Selects the newest applicable release that is newer than <paramref name="currentVersion"/>
	/// </summary>
	public static AvailableUpdate? SelectUpdate(IEnumerable<GitHubRelease> releases, ReleaseVersion currentVersion,
		bool includePrereleases, InstallType installType, bool frameworkDependent)
	{
		AvailableUpdate? best = null;
		foreach (var release in releases)
		{
			if (release.Draft || !ReleaseVersion.TryParse(release.TagName, out var version))
				continue;

			if (!includePrereleases && (release.Prerelease || version.IsPrerelease))
				continue;

			if (version.CompareTo(currentVersion) <= 0 || (best != null && version.CompareTo(best.Version) <= 0))
				continue;

			UpdateAsset? asset = null;
			if (installType is InstallType.Portable or InstallType.Msi)
			{
				// Skip releases whose files are not uploaded (yet); the release workflow attaches them after creating the release.
				var file = release.Assets.FirstOrDefault(a => IsAssetFor(a.Name, installType, frameworkDependent));
				if (file == null)
					continue;

				asset = new UpdateAsset(file.Name, file.BrowserDownloadUrl, file.Size, ParseSha256(file.Digest));
			}

			best = new AvailableUpdate(version, release.HtmlUrl, asset);
		}

		return best;
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
