using Sentinel.LogViewer.Core;
using System;
using Sentinel.LogViewer.Wpf.Models;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// The format of the timestamp column as it is saved in the configuration
/// </summary>
public static class TimeStampFormats
{
	public static string ToId(TimeStampFormat format) => format.ToString().ToLowerInvariant();

	/// <summary>
	/// Returns the format with the id; the system format when there is none, e.g. in a hand-edited file
	/// </summary>
	public static TimeStampFormat Parse(string? id) =>
		Enum.TryParse<TimeStampFormat>(id, ignoreCase: true, out var format) && Enum.IsDefined(format) ? format : TimeStampFormat.System;
}
