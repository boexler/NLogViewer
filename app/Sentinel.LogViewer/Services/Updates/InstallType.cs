using System;
using System.IO;

namespace Sentinel.LogViewer.Services.Updates;

/// <summary>
/// How the running copy of the application was installed, which decides how it is updated
/// </summary>
public enum InstallType
{
	/// <summary>Unpacked from a portable ZIP; the update replaces the files in place.</summary>
	Portable,

	/// <summary>Installed by Chocolatey; the update is left to <c>choco upgrade</c>.</summary>
	Chocolatey,

	/// <summary>Installed with the MSI; the update runs the new MSI.</summary>
	Msi,

	/// <summary>A local build (e.g. bin\Debug); updates are checked but not installed.</summary>
	Development
}

/// <summary>
/// Detects the <see cref="InstallType"/> of the running application
/// </summary>
public static class InstallTypeDetector
{
	/// <summary>Chocolatey package id; Chocolatey keeps installed packages in lib\&lt;id in lower case&gt;.</summary>
	public const string ChocolateyPackageId = "sentinel.logviewer";

	/// <summary>
	/// Detects the install type of the running application
	/// </summary>
	public static InstallType Detect()
	{
		return Detect(
			AppContext.BaseDirectory,
			Environment.GetEnvironmentVariable("ChocolateyInstall"),
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
	}

	/// <summary>
	/// Detects the install type from the given locations (for tests)
	/// </summary>
	/// <param name="applicationDirectory">Directory of the running executable</param>
	/// <param name="chocolateyInstallDirectory">Value of the ChocolateyInstall environment variable</param>
	/// <param name="programFilesDirectory">The 64-bit Program Files directory</param>
	public static InstallType Detect(string applicationDirectory, string? chocolateyInstallDirectory, string programFilesDirectory)
	{
		if (ConfigurationService.IsPortableMode(applicationDirectory))
			return InstallType.Portable;

		if (!IsInside(applicationDirectory, programFilesDirectory))
			return InstallType.Development;

		// The Chocolatey package installs the same MSI, but Chocolatey must stay aware of the installed version.
		if (!string.IsNullOrEmpty(chocolateyInstallDirectory) &&
		    Directory.Exists(Path.Combine(chocolateyInstallDirectory, "lib", ChocolateyPackageId)))
		{
			return InstallType.Chocolatey;
		}

		return InstallType.Msi;
	}

	/// <summary>
	/// Returns whether a portable copy is the framework-dependent build (it ships the app as a separate DLL)
	/// </summary>
	public static bool IsFrameworkDependent(string applicationDirectory)
	{
		return File.Exists(Path.Combine(applicationDirectory, ProductNames.Technical + ".dll"));
	}

	private static bool IsInside(string path, string directory)
	{
		if (string.IsNullOrEmpty(directory))
			return false;

		var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
	}
}
