using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// The colour of the tabs of an application: the same for the same application and the same sender, also after a restart of the viewer or of
/// the application, so it can be told at a glance which tab belongs to what
/// </summary>
public static class TabColors
{
	/// <summary>
	/// Colours that can be read on a light and on a dark background
	/// </summary>
	public static readonly string[] Palette =
	{
		"#E57373", "#F0A04B", "#D9B93C", "#A3C44D", "#7DB35E", "#3FB0A0", "#45B8C8", "#4DA3D9", "#7C83E0", "#B07CD8", "#D86FA8", "#E0805A"
	};

	/// <summary>
	/// The colour for the key of an application (its name and its sender), as <c>#RRGGBB</c>. It comes from a hash that does not change
	/// between runs and versions of .NET, which <see cref="string.GetHashCode()"/> does.
	/// </summary>
	public static string HexFor(string key)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key.ToLowerInvariant()));
		return Palette[BitConverter.ToUInt32(hash, 0) % Palette.Length];
	}

	public static Brush BrushFor(string key)
	{
		var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(HexFor(key)));
		brush.Freeze();
		return brush;
	}
}
