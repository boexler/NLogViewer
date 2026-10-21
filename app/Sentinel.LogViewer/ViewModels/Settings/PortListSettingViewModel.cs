using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Wpf;

namespace Sentinel.LogViewer.ViewModels.Settings;

/// <summary>
/// The list of addresses the application listens on. Every change is saved right away.
/// </summary>
public sealed class PortListSettingViewModel : SettingViewModel
{
	private const int FirstSuggestedPort = 4000;

	private readonly Func<IReadOnlyList<string>> _get;
	private readonly Action<List<string>> _set;
	private readonly Func<IReadOnlyList<string>> _getDefault;

	/// <param name="set">Stores and saves the addresses</param>
	public PortListSettingViewModel(string categoryId, string icon, string title, string description,
		Func<IReadOnlyList<string>> get, Action<List<string>> set, Func<IReadOnlyList<string>> getDefault,
		string invalidAddressMessage, string duplicatePortMessage)
		: base(categoryId, icon, title, description, false)
	{
		_get = get;
		_set = set;
		_getDefault = getDefault;
		InvalidAddressMessage = invalidAddressMessage;
		DuplicatePortMessage = duplicatePortMessage;
		AddCommand = new RelayCommand(Add);
		Load(_get());
	}

	public ObservableCollection<PortItemViewModel> Items { get; } = new();

	public ICommand AddCommand { get; }

	internal string InvalidAddressMessage { get; }

	internal string DuplicatePortMessage { get; }

	public override void ResetToDefault()
	{
		var defaults = _getDefault().ToList();
		_set(defaults);
		Load(defaults);
	}

	public override void Reload() => Load(_get());

	internal void Save() => _set(Items.Select(item => item.Address).ToList());

	internal void Remove(PortItemViewModel item)
	{
		Items.Remove(item);
		Save();
	}

	/// <summary>
	/// Returns whether another entry already listens on the port with the same protocol; UDP and TCP can use the same number
	/// </summary>
	internal bool IsPortInUse(string address, int port, PortItemViewModel except) =>
		Items.Any(item => !ReferenceEquals(item, except) && SettingsRules.TryGetPort(item.Address, out var other) && other == port
		                  && SettingsRules.SchemeOf(item.Address) == SettingsRules.SchemeOf(address));

	private void Load(IEnumerable<string> addresses)
	{
		Items.Clear();
		foreach (var address in addresses)
			Items.Add(new PortItemViewModel(this, address));
	}

	private void Add()
	{
		var port = FirstSuggestedPort;
		while (Items.Any(item => SettingsRules.TryGetPort(item.Address, out var used) && used == port && SettingsRules.SchemeOf(item.Address) == "udp"))
			port++;

		Items.Add(new PortItemViewModel(this, $"udp://0.0.0.0:{port}"));
		Save();
	}
}

/// <summary>
/// One address in the port list
/// </summary>
public sealed class PortItemViewModel : INotifyPropertyChanged
{
	private readonly PortListSettingViewModel _owner;
	private string _address;
	private string? _error;

	internal PortItemViewModel(PortListSettingViewModel owner, string address)
	{
		_owner = owner;
		_address = address;
		RemoveCommand = new RelayCommand(() => _owner.Remove(this));
	}

	/// <summary>
	/// The address, e.g. <c>udp://0.0.0.0:4000</c>. An invalid or duplicate input is not saved and shows an error instead.
	/// </summary>
	public string Address
	{
		get => _address;
		set
		{
			var address = value?.Trim() ?? string.Empty;
			if (!SettingsRules.TryGetPort(address, out var port))
			{
				// The text box keeps what was typed, so nothing is raised for the address.
				Error = _owner.InvalidAddressMessage;
				return;
			}

			if (_owner.IsPortInUse(address, port, this))
			{
				Error = _owner.DuplicatePortMessage;
				return;
			}

			Error = null;
			if (address != _address)
			{
				_address = address;
				_owner.Save();
			}

			OnPropertyChanged();
		}
	}

	public string? Error
	{
		get => _error;
		private set
		{
			if (_error == value)
				return;

			_error = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(HasError));
		}
	}

	public bool HasError => _error != null;

	public ICommand RemoveCommand { get; }

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
