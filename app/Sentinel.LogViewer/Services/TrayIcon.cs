using System;
using System.IO;
using System.Windows;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// The icon of the application in the notification area, which the window uses while it is minimized to the tray
/// </summary>
public interface ITrayIcon
{
	/// <summary>Raised when the user wants the window back: double-click, a click on a notification or the entry of the menu</summary>
	event EventHandler? RestoreRequested;

	/// <summary>Raised when the user chose to exit in the menu of the icon</summary>
	event EventHandler? ExitRequested;

	bool IsVisible { get; }

	/// <summary>The text that shows when the pointer rests on the icon</summary>
	string ToolTip { set; }

	void Show();

	void Hide();

	/// <summary>Shows a notification that stands out from the icon</summary>
	void Notify(string title, string text);
}

/// <summary>
/// The tray icon of Windows (<see cref="System.Windows.Forms.NotifyIcon"/>, part of the .NET desktop runtime)
/// </summary>
public sealed class NotifyIconTray : ITrayIcon, IDisposable
{
	/// <summary>Windows cuts the tool tip of a notify icon after this many characters</summary>
	private const int MaxToolTipLength = 127;

	private readonly System.Windows.Forms.NotifyIcon _icon;

	public NotifyIconTray(LocalizationService localization)
	{
		using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Sentinel.LogViewer;component/Resources/icons/icons8-analysieren-64.ico"))!.Stream;
		_icon = new System.Windows.Forms.NotifyIcon { Icon = new System.Drawing.Icon(stream), Visible = false };
		_icon.DoubleClick += (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty);
		_icon.BalloonTipClicked += (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty);

		var menu = new System.Windows.Forms.ContextMenuStrip();
		menu.Items.Add(localization.GetString("Tray_Open", "Open"), null, (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty));
		menu.Items.Add(localization.GetString("Tray_Exit", "Exit"), null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));
		_icon.ContextMenuStrip = menu;
	}

	public event EventHandler? RestoreRequested;

	public event EventHandler? ExitRequested;

	public bool IsVisible => _icon.Visible;

	public string ToolTip
	{
		set => _icon.Text = value.Length > MaxToolTipLength ? value[..MaxToolTipLength] : value;
	}

	public void Show() => _icon.Visible = true;

	public void Hide() => _icon.Visible = false;

	public void Notify(string title, string text) => _icon.ShowBalloonTip(5000, title, text, System.Windows.Forms.ToolTipIcon.Error);

	public void Dispose()
	{
		_icon.Visible = false;
		_icon.Dispose();
	}
}
