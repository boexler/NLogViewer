using System;

namespace Sentinel.LogViewer.Services.Updates;

/// <summary>
/// GitHub refused a request of the API because the limit of requests per hour is used up
/// </summary>
/// <param name="ResetAt">When requests are allowed again, or null when GitHub did not say</param>
public sealed class GitHubRateLimitException(DateTimeOffset? resetAt) : Exception("GitHub's limit of requests per hour is reached.")
{
	public DateTimeOffset? ResetAt { get; } = resetAt;
}

/// <summary>
/// The release has no file for this installation, e.g. because the release workflow is still uploading them
/// </summary>
public sealed class ReleaseFilesMissingException(string tag) : Exception($"The release {tag} has no file for this installation.");
