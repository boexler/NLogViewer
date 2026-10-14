using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sentinel.LogViewer.Services.Updates;

/// <summary>
/// A release version compared by SemVer 2 precedence (build metadata is ignored)
/// </summary>
public sealed class ReleaseVersion : IComparable<ReleaseVersion>
{
	private static readonly Regex VersionRegex = new(
		@"^[vV]?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-(?<pre>[0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$",
		RegexOptions.Compiled);

	private readonly string[] _prereleaseIdentifiers;

	private ReleaseVersion(int major, int minor, int patch, string prerelease)
	{
		Major = major;
		Minor = minor;
		Patch = patch;
		Prerelease = prerelease;
		_prereleaseIdentifiers = prerelease.Length == 0 ? Array.Empty<string>() : prerelease.Split('.');
	}

	public int Major { get; }
	public int Minor { get; }
	public int Patch { get; }

	/// <summary>Prerelease label without the leading dash, empty for a stable version</summary>
	public string Prerelease { get; }

	public bool IsPrerelease => Prerelease.Length > 0;

	/// <summary>
	/// Parses versions like <c>5.0.0</c>, <c>v5.0.0-beta.1</c> or <c>5.0.0-beta.1+abc123</c>
	/// </summary>
	public static bool TryParse(string? text, out ReleaseVersion version)
	{
		version = null!;
		var match = VersionRegex.Match(text?.Trim() ?? string.Empty);
		if (!match.Success ||
		    !int.TryParse(match.Groups["major"].Value, out var major) ||
		    !int.TryParse(match.Groups["minor"].Value, out var minor) ||
		    !int.TryParse(match.Groups["patch"].Value, out var patch))
		{
			return false;
		}

		version = new ReleaseVersion(major, minor, patch, match.Groups["pre"].Value);
		return true;
	}

	public int CompareTo(ReleaseVersion? other)
	{
		if (other is null)
			return 1;

		var result = Major.CompareTo(other.Major);
		if (result == 0) result = Minor.CompareTo(other.Minor);
		if (result == 0) result = Patch.CompareTo(other.Patch);
		if (result != 0)
			return result;

		// A stable version is greater than any prerelease of the same version.
		if (!IsPrerelease || !other.IsPrerelease)
			return other.IsPrerelease.CompareTo(IsPrerelease);

		var length = Math.Min(_prereleaseIdentifiers.Length, other._prereleaseIdentifiers.Length);
		for (var i = 0; i < length; i++)
		{
			result = CompareIdentifiers(_prereleaseIdentifiers[i], other._prereleaseIdentifiers[i]);
			if (result != 0)
				return result;
		}

		return _prereleaseIdentifiers.Length.CompareTo(other._prereleaseIdentifiers.Length);
	}

	private static int CompareIdentifiers(string left, string right)
	{
		var leftIsNumber = left.All(char.IsDigit);
		var rightIsNumber = right.All(char.IsDigit);
		if (leftIsNumber && rightIsNumber)
			return left.Length != right.Length ? left.Length.CompareTo(right.Length) : string.CompareOrdinal(left, right);

		// Numeric identifiers have lower precedence than alphanumeric ones.
		if (leftIsNumber != rightIsNumber)
			return leftIsNumber ? -1 : 1;

		return string.CompareOrdinal(left, right);
	}

	public override string ToString()
	{
		var version = $"{Major}.{Minor}.{Patch}";
		return IsPrerelease ? $"{version}-{Prerelease}" : version;
	}
}
