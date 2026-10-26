using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Wpf;

namespace Sentinel.LogViewer.ViewModels;

/// <summary>
/// The dialog for the time zones of a tab: the zone in which the times of a file were written, and the zone in which they are shown
/// </summary>
public class TimeZoneViewModel : INotifyPropertyChanged
{
	private readonly LocalizationService _localization;
	private readonly LogEntry? _sample;
	private TimeZoneChoice _selectedFileZone;
	private TimeZoneChoice _selectedDisplayZone;

	/// <param name="isLive">Whether the tab receives logs over the network; their times have a zone, so there is no zone of the file to choose</param>
	/// <param name="fileZone">The zone of the file now; null for the local time</param>
	/// <param name="displayZone">The zone that the times are shown in now; null for the local time</param>
	/// <param name="sample">An entry of the tab for the preview, if there is one</param>
	public TimeZoneViewModel(bool isLive, TimeZoneInfo? fileZone, TimeZoneInfo? displayZone, LogEntry? sample, LocalizationService localization)
	{
		_localization = localization ?? throw new ArgumentNullException(nameof(localization));
		_sample = sample;
		ShowFileZone = !isLive;
		Zones = TimeZoneChoices.Create(localization);
		_selectedFileZone = TimeZoneChoices.Find(Zones, fileZone);
		_selectedDisplayZone = TimeZoneChoices.Find(Zones, displayZone);
		ResetCommand = new RelayCommand(() =>
		{
			SelectedFileZone = TimeZoneChoices.Find(Zones, null);
			SelectedDisplayZone = TimeZoneChoices.Find(Zones, null);
		});
	}

	public IReadOnlyList<TimeZoneChoice> Zones { get; }

	/// <summary>Whether the zone of the file can be chosen: not for a tab of logs from the network</summary>
	public bool ShowFileZone { get; }

	public TimeZoneChoice SelectedFileZone
	{
		get => _selectedFileZone;
		set
		{
			if (value == null || _selectedFileZone == value)
				return;

			_selectedFileZone = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(Preview));
		}
	}

	public TimeZoneChoice SelectedDisplayZone
	{
		get => _selectedDisplayZone;
		set
		{
			if (value == null || _selectedDisplayZone == value)
				return;

			_selectedDisplayZone = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(Preview));
		}
	}

	/// <summary>The zone of the file that is chosen; null for the local time</summary>
	public TimeZoneInfo? FileZone => SelectedFileZone.Zone;

	/// <summary>The zone to show the times in that is chosen; null for the local time</summary>
	public TimeZoneInfo? DisplayZone => SelectedDisplayZone.Zone;

	public ICommand ResetCommand { get; }

	/// <summary>The time of the sample entry as it is written and as it is shown, e.g. <c>2025-12-18 14:02:11 (UTC+9) → 2025-12-18 07:02:11 (local)</c></summary>
	public string Preview
	{
		get
		{
			if (_sample == null)
				return string.Empty;

			var fileZone = ShowFileZone ? FileZone : null;
			var written = _sample.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
			var shown = EntryTime.ToDisplay(_sample.TimeStamp, fileZone, DisplayZone).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
			return $"{written} ({EntryTime.Label(EntryTime.ZoneOf(_sample.TimeStamp, fileZone))}) → {shown} ({EntryTime.Label(DisplayZone)})";
		}
	}

	public string PreviewTitle => _localization.GetString("TimeZone_Preview", "Preview");

	public event PropertyChangedEventHandler? PropertyChanged;

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
