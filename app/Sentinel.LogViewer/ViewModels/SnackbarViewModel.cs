using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Sentinel.LogViewer.Wpf;

namespace Sentinel.LogViewer.ViewModels;

public enum SnackbarKind
{
	/// <summary>A hint that goes away by itself</summary>
	Info,

	/// <summary>Something went wrong; it stays until the user closes it</summary>
	Error
}

/// <summary>
/// The small card at the bottom of the window that tells the user something for a moment. Only one is shown at a time; a new message
/// takes the place of the one that is there.
/// </summary>
public sealed class SnackbarViewModel : INotifyPropertyChanged
{
	private readonly DispatcherTimer _timer = new();
	private Action? _undo;
	private string _message = string.Empty;
	private SnackbarKind _kind;
	private string? _undoText;
	private bool _isOpen;

	public SnackbarViewModel()
	{
		_timer.Tick += (_, _) => Close();
		UndoCommand = new RelayCommand(() =>
		{
			var undo = _undo;
			Close();
			undo?.Invoke();
		});
		CloseCommand = new RelayCommand(Close);
	}

	/// <summary>How long a hint stays</summary>
	public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(4);

	/// <summary>How long a hint with an undo button stays; the user needs time to decide</summary>
	public TimeSpan DurationWithUndo { get; set; } = TimeSpan.FromSeconds(6);

	public string Message
	{
		get => _message;
		private set => Set(ref _message, value);
	}

	public SnackbarKind Kind
	{
		get => _kind;
		private set
		{
			if (Set(ref _kind, value))
				OnPropertyChanged(nameof(IsError));
		}
	}

	public bool IsError => _kind == SnackbarKind.Error;

	/// <summary>The text of the undo button, or null when there is nothing to undo</summary>
	public string? UndoText
	{
		get => _undoText;
		private set
		{
			if (Set(ref _undoText, value))
				OnPropertyChanged(nameof(HasUndo));
		}
	}

	public bool HasUndo => _undoText != null;

	public bool IsOpen
	{
		get => _isOpen;
		private set => Set(ref _isOpen, value);
	}

	/// <summary>
	/// Whether the card may move; Windows has a setting that switches animations off
	/// </summary>
	public bool AnimationsEnabled => SystemParameters.ClientAreaAnimation;

	public ICommand UndoCommand { get; }

	public ICommand CloseCommand { get; }

	/// <summary>
	/// Shows a hint that goes away by itself; with <paramref name="undo"/> it has a button that takes back what was done
	/// </summary>
	public void Show(string message, string? undoText = null, Action? undo = null) =>
		Open(message, SnackbarKind.Info, undoText, undo);

	/// <summary>
	/// Shows an error; it stays until the user closes it
	/// </summary>
	public void ShowError(string message) => Open(message, SnackbarKind.Error, null, null);

	public void Close()
	{
		_timer.Stop();
		_undo = null;
		IsOpen = false;
	}

	private void Open(string message, SnackbarKind kind, string? undoText, Action? undo)
	{
		_timer.Stop();
		_undo = undo;
		Message = message;
		Kind = kind;
		UndoText = undo == null ? null : undoText;
		IsOpen = true;

		if (kind == SnackbarKind.Info)
		{
			_timer.Interval = undo == null ? Duration : DurationWithUndo;
			_timer.Start();
		}
	}

	private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (Equals(field, value))
			return false;

		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
