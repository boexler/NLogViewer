using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// The colours that mark the tabs of the applications that send logs. There are 16, spread evenly over the hues in the OKLCH colour space
/// with the same lightness and chroma, so no two of them look alike. Each has a dark shade (the stripe and the dot) and a light shade
/// (the tint of the tab).
/// </summary>
public static class TabColors
{
	public const int Count = 16;

	private const double DarkLightness = 0.60;
	private const double DarkChroma = 0.14;
	private const double LightLightness = 0.93;
	private const double LightChroma = 0.04;

	// The first hue is a red; the others follow in equal steps.
	private const double FirstHue = 25;

	private static readonly string[] DarkHex = Make(DarkLightness, DarkChroma);
	private static readonly string[] LightHex = Make(LightLightness, LightChroma);

	/// <summary>The dark shade of a colour as <c>#RRGGBB</c>; the number wraps around</summary>
	public static string Dark(int index) => DarkHex[Wrap(index)];

	/// <summary>The light shade of a colour as <c>#RRGGBB</c>; the number wraps around</summary>
	public static string Light(int index) => LightHex[Wrap(index)];

	public static Brush DarkBrush(int index) => Freeze(Dark(index));

	public static Brush LightBrush(int index) => Freeze(Light(index));

	/// <summary>A tint between the light and the dark shade, for the tab that is selected</summary>
	public static Brush StrongBrush(int index)
	{
		var light = (Color)ColorConverter.ConvertFromString(Light(index));
		var dark = (Color)ColorConverter.ConvertFromString(Dark(index));
		const double darkShare = 0.25;
		byte Mix(byte from, byte to) => (byte)Math.Round(from + (to - from) * darkShare);
		var brush = new SolidColorBrush(Color.FromRgb(Mix(light.R, dark.R), Mix(light.G, dark.G), Mix(light.B, dark.B)));
		brush.Freeze();
		return brush;
	}

	/// <summary>
	/// The colour for a tab of an application. An application that has a tab open keeps its colour in the new tabs. A new one takes a colour
	/// that no open tab shows, and when all of them are shown, one that is shown least; of those the one that differs most from the colours of
	/// the open tabs (the greatest distance in hue to the nearest of them, then to all of them), so that the tabs are easy to tell apart.
	/// Nothing is saved: after a restart the colours are given again in the order the applications send.
	/// </summary>
	/// <param name="key">The key of the application of the new tab</param>
	/// <param name="open">The key and the colour of every open tab of an application</param>
	public static int Choose(string key, IEnumerable<(string Key, int Colour)> open)
	{
		var shown = new int[Count];
		foreach (var (otherKey, colour) in open)
		{
			if (colour is < 0 or >= Count)
				continue;

			if (otherKey == key)
				return colour;

			shown[colour]++;
		}

		var least = shown.Min();
		var best = -1;
		var bestNearest = -1;
		var bestTotal = -1;
		for (var candidate = 0; candidate < Count; candidate++)
		{
			if (shown[candidate] != least)
				continue;

			// The colours are 22.5 degrees apart on a circle of hues, so the distance of two of them is the shorter way round.
			var nearest = Count;
			var total = 0;
			for (var other = 0; other < Count; other++)
			{
				if (other == candidate || shown[other] == 0)
					continue;

				var distance = Math.Min(Math.Abs(candidate - other), Count - Math.Abs(candidate - other));
				nearest = Math.Min(nearest, distance);
				total += distance * shown[other];
			}

			// A tie goes to the lower number, so the result does not change from one run to the next.
			if (nearest > bestNearest || (nearest == bestNearest && total > bestTotal))
			{
				best = candidate;
				bestNearest = nearest;
				bestTotal = total;
			}
		}

		return best;
	}

	private static int Wrap(int index) => ((index % Count) + Count) % Count;

	private static Brush Freeze(string hex)
	{
		var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
		brush.Freeze();
		return brush;
	}

	private static string[] Make(double lightness, double chroma)
	{
		var colours = new string[Count];
		for (var i = 0; i < Count; i++)
			colours[i] = ToHex(lightness, chroma, FirstHue + i * 360.0 / Count);

		return colours;
	}

	/// <summary>
	/// OKLCH to sRGB (Björn Ottosson's matrices); a colour outside of the sRGB range is cut to it
	/// </summary>
	private static string ToHex(double lightness, double chroma, double hueDegrees)
	{
		var hue = hueDegrees * Math.PI / 180;
		var a = chroma * Math.Cos(hue);
		var b = chroma * Math.Sin(hue);

		var l = Math.Pow(lightness + 0.3963377774 * a + 0.2158037573 * b, 3);
		var m = Math.Pow(lightness - 0.1055613458 * a - 0.0638541728 * b, 3);
		var s = Math.Pow(lightness - 0.0894841775 * a - 1.2914855480 * b, 3);

		var r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
		var g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
		var blue = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;

		return string.Create(CultureInfo.InvariantCulture, $"#{Channel(r):X2}{Channel(g):X2}{Channel(blue):X2}");
	}

	private static int Channel(double linear)
	{
		var clamped = Math.Clamp(linear, 0, 1);
		var encoded = clamped <= 0.0031308 ? 12.92 * clamped : 1.055 * Math.Pow(clamped, 1 / 2.4) - 0.055;
		return (int)Math.Round(Math.Clamp(encoded, 0, 1) * 255);
	}
}
