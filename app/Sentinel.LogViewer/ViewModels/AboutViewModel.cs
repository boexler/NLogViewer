using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Updates;
using Sentinel.LogViewer.Wpf;

namespace Sentinel.LogViewer.ViewModels;

/// <summary>
/// States of the update line in the About dialog
/// </summary>
public enum UpdateState
{
	Checking,
	UpToDate,
	UpdateAvailable,
	Downloading,
	ReadyToInstall,
	/// <summary>An update exists but is installed outside the app (Chocolatey, development build).</summary>
	ManualUpdate,
	Failed
}

/// <summary>
/// ViewModel for the About dialog, which also checks for, downloads and installs updates
/// </summary>
public class AboutViewModel : INotifyPropertyChanged
{
	private const double BytesPerMegabyte = 1024 * 1024;

	private readonly UpdateService _updateService;
	private readonly ConfigurationService _configService;
	private readonly LocalizationService _localizationService;
	private readonly ChangelogService _changelogService;
	private UpdateState _state = UpdateState.Checking;
	private string _statusText = string.Empty;
	private double _downloadPercentage;
	private AvailableUpdate? _update;
	private string? _downloadedFile;

	public AboutViewModel(UpdateService updateService, ConfigurationService configService, LocalizationService localizationService,
		ChangelogService changelogService)
	{
		_changelogService = changelogService ?? throw new ArgumentNullException(nameof(changelogService));
		_updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
		_configService = configService ?? throw new ArgumentNullException(nameof(configService));
		_localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

		CheckCommand = new AsyncRelayCommand(CheckAsync);
		DownloadCommand = new AsyncRelayCommand(DownloadAsync);
		RestartCommand = new RelayCommand(Restart);
		CopyChocolateyCommand = new RelayCommand(() => Clipboard.SetText(ChocolateyCommand));
		ShowChangelogCommand = new RelayCommand(() => ChangelogWindow.Show(Application.Current.Windows.OfType<AboutWindow>().FirstOrDefault()));
	}

	public string ProductName => ProductNames.Display;

	public string VersionText =>
		$"{UpdateService.CurrentVersion} ({(Environment.Is64BitProcess ? "64" : "32")}-{GetString("About_Bit", "bit")})";

	public string ProjectUrl => UpdateService.RepositoryUrl;

	public string IssuesUrl => $"{UpdateService.RepositoryUrl}/issues";

	public string LicenseUrl => $"{UpdateService.RepositoryUrl}/blob/master/LICENSE.md";

	public string ReleasesUrl => _update?.ReleasePageUrl ?? $"{UpdateService.RepositoryUrl}/releases";

	public string ChocolateyCommand =>
		_update?.Version.IsPrerelease == true
			? "choco upgrade Sentinel.LogViewer --pre -y"
			: "choco upgrade Sentinel.LogViewer -y";

	public UpdateState State
	{
		get => _state;
		private set
		{
			_state = value;
			// Every flag below depends on the state.
			OnPropertyChanged(string.Empty);
		}
	}

	public string StatusText
	{
		get => _statusText;
		private set
		{
			_statusText = value;
			OnPropertyChanged();
		}
	}

	public double DownloadPercentage
	{
		get => _downloadPercentage;
		private set
		{
			_downloadPercentage = value;
			OnPropertyChanged();
		}
	}

	public bool IsChecking => State == UpdateState.Checking;
	public bool IsUpToDate => State == UpdateState.UpToDate;
	public bool CanDownload => State == UpdateState.UpdateAvailable;
	public bool IsDownloading => State == UpdateState.Downloading;
	public bool CanRestart => State == UpdateState.ReadyToInstall;
	public bool ShowChocolateyCommand => State == UpdateState.ManualUpdate && _updateService.InstallType == InstallType.Chocolatey;
	public bool ShowReleaseLink => State == UpdateState.ManualUpdate && _updateService.InstallType != InstallType.Chocolatey;
	public bool CanRetry => State == UpdateState.Failed;

	public ICommand CheckCommand { get; }
	public ICommand DownloadCommand { get; }
	public ICommand RestartCommand { get; }
	public ICommand CopyChocolateyCommand { get; }

	/// <summary>Opens the changelog of all versions</summary>
	public ICommand ShowChangelogCommand { get; }

	/// <summary>
	/// Checks GitHub Releases for a newer version and updates the status line
	/// </summary>
	public async Task CheckAsync()
	{
		State = UpdateState.Checking;
		StatusText = GetString("About_Checking", "Checking for updates…");
		try
		{
			_update = await _updateService.CheckForUpdateAsync(UpdateService.IncludesPrereleases(_configService.LoadConfiguration()));
		}
		catch (Exception ex)
		{
			ShowError(ex);
			return;
		}

		if (_update == null)
		{
			State = UpdateState.UpToDate;
			StatusText = Format("About_UpToDate", "{0} is up to date", ProductNames.Display);
		}
		else if (_update.Asset == null)
		{
			State = UpdateState.ManualUpdate;
			StatusText = _updateService.InstallType == InstallType.Chocolatey
				? Format("About_ChocolateyUpdate", "Update to {0} available. Install it with Chocolatey:", _update.Version)
				: Format("About_ManualUpdate", "Update to {0} available. This copy cannot update itself.", _update.Version);
		}
		else
		{
			State = UpdateState.UpdateAvailable;
			StatusText = Format("About_UpdateAvailable", "Update to {0} available", _update.Version);
		}
	}

	private async Task DownloadAsync()
	{
		if (_update?.Asset == null)
			return;

		State = UpdateState.Downloading;
		DownloadPercentage = 0;
		var total = _update.Asset.Size;
		StatusText = FormatDownload(0, total);
		var progress = new Progress<(long Downloaded, long Total)>(p =>
		{
			DownloadPercentage = p.Total > 0 ? p.Downloaded * 100.0 / p.Total : 0;
			StatusText = FormatDownload(p.Downloaded, p.Total);
		});

		try
		{
			_downloadedFile = await _updateService.DownloadAsync(_update, progress);
		}
		catch (Exception ex)
		{
			ShowError(ex);
			return;
		}

		// The notes of the new version are saved, so that they also show after the restart without a network.
		_ = _changelogService.LoadAsync();

		State = UpdateState.ReadyToInstall;
		StatusText = Format("About_ReadyToInstall", "Update to {0} downloaded", _update.Version);
	}

	private void Restart()
	{
		if (_downloadedFile == null)
			return;

		try
		{
			_updateService.StartInstallation(_downloadedFile);
		}
		catch (Exception ex)
		{
			ShowError(ex);
			return;
		}

		Application.Current.Shutdown();
	}

	private void ShowError(Exception ex)
	{
		State = UpdateState.Failed;
		StatusText = Format("About_UpdateFailed", "Update failed: {0}", ex.Message);
	}

	private string FormatDownload(long downloaded, long total)
	{
		return Format("About_Downloading", "Downloading update – {0} of {1} MB",
			(downloaded / BytesPerMegabyte).ToString("0.0", CultureInfo.CurrentCulture),
			(total / BytesPerMegabyte).ToString("0.0", CultureInfo.CurrentCulture));
	}

	private string GetString(string key, string defaultValue) => _localizationService.GetString(key, defaultValue);

	private string Format(string key, string defaultValue, params object[] arguments) =>
		string.Format(CultureInfo.CurrentCulture, GetString(key, defaultValue), arguments);

	public event PropertyChangedEventHandler? PropertyChanged;

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
