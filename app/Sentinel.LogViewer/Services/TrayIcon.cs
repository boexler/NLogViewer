using System;
using System.IO;
using System.Windows;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// The icon of the application in the notification area, which the window uses while it is minimized to the tray
/// </summary>
public interface ITrayIcon
{
	/// <summary>Raised when the user wants the window back: double-click or the entry of the menu</summary>
	event EventHandler? RestoreRequested;

	/// <summary>Raised when the user clicked the notification that was shown last</summary>
	event EventHandler? NotificationClicked;

	/// <summary>Raised when the user chose to exit in the menu of the icon</summary>
	event EventHandler? ExitRequested;

	bool IsVisible { get; }

	/// <summary>The text that shows when the pointer rests on the icon</summary>
	string ToolTip { set; }

	void Show();

	void Hide();

	/// <summary>Shows a notification that stands out from the icon, with the symbol of a warning or of an error</summary>
	void Notify(string title, string text, bool isWarning);
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
		_icon.BalloonTipClicked += (_, _) => NotificationClicked?.Invoke(this, EventArgs.Empty);

		var menu = new System.Windows.Forms.ContextMenuStrip();
		var open = menu.Items.Add(string.Empty, null, (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty));
		var exit = menu.Items.Add(string.Empty, null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));
		_icon.ContextMenuStrip = menu;

		void SetTexts()
		{
			open.Text = localization.GetString("Tray_Open", "Open");
			exit.Text = localization.GetString("Tray_Exit", "Exit");
		}

		SetTexts();
		localization.PropertyChanged += (_, _) => SetTexts();
	}

	public event EventHandler? RestoreRequested;

	public event EventHandler? NotificationClicked;

	public event EventHandler? ExitRequested;

	public bool IsVisible => _icon.Visible;

	public string ToolTip
	{
		set => _icon.Text = value.Length > MaxToolTipLength ? value[..MaxToolTipLength] : value;
	}

	public void Show() => _icon.Visible = true;

	public void Hide() => _icon.Visible = false;

	public void Notify(string title, string text, bool isWarning) =>
		_icon.ShowBalloonTip(5000, title, text, isWarning ? System.Windows.Forms.ToolTipIcon.Warning : System.Windows.Forms.ToolTipIcon.Error);

	public void Dispose()
	{
		_icon.Visible = false;
		_icon.Dispose();
	}
}
