using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// Resolves the process (name and PID) that is using a given port on Windows.
/// </summary>
public static class PortProcessResolver
{
	/// <summary>
	/// Tries to get the process name and PID that is using the given port.
	/// Only supported on Windows; uses netstat output parsing.
	/// </summary>
	/// <param name="port">Port number (e.g. 4000).</param>
	/// <param name="udp">True for UDP, false for TCP.</param>
	/// <returns>Process name and PID if found; (null, null) on non-Windows, parse failure, or when process has exited.</returns>
	public static (string? processName, int? pid) TryGetProcessUsingPort(int port, bool udp = true)
	{
		if (!OperatingSystem.IsWindows())
			return (null, null);

		try
		{
			var pid = TryGetPidFromNetStat(port, udp);
			if (pid == null)
				return (null, null);

			string? processName = null;
			try
			{
				using var process = Process.GetProcessById(pid.Value);
				processName = process.ProcessName;
			}
			catch (ArgumentException)
			{
				// Process may have exited or access denied
			}

			return (processName, pid);
		}
		catch
		{
			return (null, null);
		}
	}

	private static int? TryGetPidFromNetStat(int port, bool udp)
	{
		try
		{
			var netstatPath = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.System),
				"netstat.exe");
			if (!File.Exists(netstatPath))
				netstatPath = "netstat.exe";

			using var process = new Process();
			process.StartInfo = new ProcessStartInfo
			{
				FileName = netstatPath,
				Arguments = "-a -n -o",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
				// Netstat outputs in the console/OEM code page on Windows
				StandardOutputEncoding = GetConsoleOutputEncoding(),
				StandardErrorEncoding = GetConsoleOutputEncoding()
			};
			process.Start();
			var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
			process.WaitForExit(5000);

			var protocol = udp ? "UDP" : "TCP";
			// Match local address containing :port (e.g. 0.0.0.0:4000 or [::]:4000)
			var portPattern = $":{port}\\b";
			var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

			foreach (var line in lines)
			{
				var trimmed = line.TrimStart();
				if (!trimmed.StartsWith(protocol, StringComparison.OrdinalIgnoreCase))
					continue;
				if (!Regex.IsMatch(line, portPattern))
					continue;

				// Last column is PID; extract last integer on the line
				var pid = ExtractLastPidFromLine(line);
				if (pid != null)
					return pid;
			}
		}
		catch
		{
			// Ignore
		}

		return null;
	}

	/// <summary>
	/// Extracts the last integer (PID) from a netstat line. Handles variable spacing.
	/// </summary>
	private static int? ExtractLastPidFromLine(string line)
	{
		var tokens = Regex.Split(line.Trim(), @"\s+");
		for (var i = tokens.Length - 1; i >= 0; i--)
		{
			if (string.IsNullOrEmpty(tokens[i]))
				continue;
			if (int.TryParse(tokens[i], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && pid > 0)
				return pid;
			break;
		}
		return null;
	}

	private static Encoding GetConsoleOutputEncoding()
	{
		if (!OperatingSystem.IsWindows())
			return Encoding.UTF8;
		try
		{
			// Netstat outputs in the console (OEM) code page on Windows
			var oemCodePage = CultureInfo.CurrentCulture.TextInfo.OEMCodePage;
			return Encoding.GetEncoding(oemCodePage);
		}
		catch
		{
			return Encoding.Default;
		}
	}
}
