using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Sentinel.LogViewer.Helpers;

/// <summary>
/// Switches the title bar of a window between light and dark; Windows draws it, so the theme brushes do not reach it
/// </summary>
public static class DarkTitleBar
{
	// The attribute has the value 19 before Windows 10 version 20H1 and 20 afterwards.
	private const int ImmersiveDarkModeBeforeVersion20H1 = 19;
	private const int ImmersiveDarkMode = 20;

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

	/// <summary>
	/// Does nothing as long as the window has no handle yet; apply it again when the window has loaded
	/// </summary>
	public static void Apply(Window window, bool dark)
	{
		var handle = new WindowInteropHelper(window).Handle;
		if (handle == IntPtr.Zero)
			return;

		var value = dark ? 1 : 0;
		try
		{
			if (DwmSetWindowAttribute(handle, ImmersiveDarkMode, ref value, sizeof(int)) != 0)
				DwmSetWindowAttribute(handle, ImmersiveDarkModeBeforeVersion20H1, ref value, sizeof(int));
		}
		catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
		{
			// Without the desktop window manager the title bar stays as it is.
		}
	}
}
