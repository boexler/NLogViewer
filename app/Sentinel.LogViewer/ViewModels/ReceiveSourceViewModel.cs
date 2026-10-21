using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Wpf;

namespace Sentinel.LogViewer.ViewModels;

/// <summary>
/// What the status bar says about receiving as a whole
/// </summary>
public enum ReceiveStatus
{
	/// <summary>Nothing was started yet</summary>
	Ready,

	/// <summary>Every source receives</summary>
	Active,

	/// <summary>Some sources receive and some do not</summary>
	Partial,

	/// <summary>Receiving was stopped</summary>
	Stopped,

	/// <summary>No source could be started</summary>
	Failed
}

/// <summary>
/// A row of the sources panel: one source with its state, its numbers and a switch to turn it on and off
/// </summary>
public sealed class ReceiveSourceViewModel : INotifyPropertyChanged
{
	private readonly LogReceiverService _receiver;
	private readonly LocalizationService? _localization;

	public ReceiveSourceViewModel(ReceiveSource source, LogReceiverService receiver, LocalizationService? localization = null)
	{
		Source = source;
		_receiver = receiver;
		_localization = localization;
		RetryCommand = new RelayCommand(() =>
		{
			_receiver.Start(Source);
			Refresh();
		});
	}

	public ReceiveSource Source { get; }

	public string Name => Source.Name;

	public string Kind => Source.Kind;

	public ReceiveSourceState State => Source.State;

	/// <summary>
	/// Whether the source receives; setting it starts or stops the source
	/// </summary>
	public bool IsOn
	{
		get => Source.State == ReceiveSourceState.Listening;
		set
		{
			if (value == IsOn)
				return;

			if (value)
				_receiver.Start(Source);
			else
				_receiver.Stop(Source);

			Refresh();
		}
	}

	public string StateText => Source.State switch
	{
		ReceiveSourceState.Listening => Text("Source_Listening", "Listening"),
		ReceiveSourceState.Failed when Source.Failure == ReceiveSourceFailure.PortInUse => Source.BlockingProcess is { } process
			? string.Format(CultureInfo.CurrentCulture, Text("Source_PortInUseBy", "Port in use by {0}"), process)
			: Text("Source_PortInUse", "Port in use"),
		ReceiveSourceState.Failed => Text("Source_Failed", "Could not start"),
		_ => Text("Source_Stopped", "Stopped")
	};

	/// <summary>The message of a failure, for the tooltip</summary>
	public string? ErrorDetail => Source.Error;

	public string EntryCountText => Source.EntryCount.ToString("N0", CultureInfo.CurrentCulture);

	public string LastEntryText => Source.LastEntryUtc is { } last ? last.ToLocalTime().ToString("T", CultureInfo.CurrentCulture) : "–";

	public bool CanRetry => Source.State == ReceiveSourceState.Failed;

	public ICommand RetryCommand { get; }

	/// <summary>
	/// Reads the state and the numbers of the source again
	/// </summary>
	public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

	private string Text(string key, string defaultValue) => _localization?.GetString(key, defaultValue) ?? defaultValue;

	public event PropertyChangedEventHandler? PropertyChanged;
}
