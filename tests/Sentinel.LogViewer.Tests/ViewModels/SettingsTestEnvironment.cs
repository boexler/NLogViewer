using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Theming;
using Sentinel.LogViewer.ViewModels;
using Sentinel.LogViewer.ViewModels.Settings;
using Xunit;

namespace Sentinel.LogViewer.Tests.ViewModels;

/// <summary>
/// Collection for tests that change the culture of the process, so no other test runs at the same time
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class CultureCollection
{
	public const string Name = "Culture";
}

/// <summary>
/// Answers the dialogs of the settings window without showing anything
/// </summary>
public sealed class FakeSettingsDialogs : ISettingsDialogs
{
	public bool ConfirmAnswer { get; set; } = true;
	public string? ExportPath { get; set; }
	public string? ImportPath { get; set; }
	public List<string> Confirmations { get; } = new();
	public List<string> Errors { get; } = new();

	public bool Confirm(string caption, string message)
	{
		Confirmations.Add(message);
		return ConfirmAnswer;
	}

	public void ShowError(string caption, string message) => Errors.Add(message);

	public string? PickFileToExport(string title, string filter, string defaultFileName) => ExportPath;

	public string? PickFileToImport(string title, string filter) => ImportPath;
}

/// <summary>
/// Configuration, saved column mappings and settings view model in a temporary folder
/// </summary>
public sealed class SettingsTestEnvironment : IDisposable
{
	public SettingsTestEnvironment()
	{
		Directory = System.IO.Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nlv_settings_" + Guid.NewGuid().ToString("N"))).FullName;
		// The marker keeps the configuration in the temporary folder instead of the user's AppData.
		File.WriteAllText(Path.Combine(Directory, ConfigurationService.PortableMarkerDotFileName), string.Empty);

		Configuration = new ConfigurationService(Directory);
		Formats = new TextFileFormatConfigService(Path.Combine(Directory, "textfileformats.json"));
		Localization = new LocalizationService(Configuration);
		Transfer = new SettingsTransferService(Configuration, Formats);
	}

	public string Directory { get; }
	public ConfigurationService Configuration { get; }
	public TextFileFormatConfigService Formats { get; }
	public LocalizationService Localization { get; }
	public SettingsTransferService Transfer { get; }
	public FakeSettingsDialogs Dialogs { get; } = new();

	/// <summary>
	/// What is saved on disk right now
	/// </summary>
	public AppConfiguration Saved => Configuration.LoadConfiguration();

	public SettingsViewModel CreateViewModel() => new(Configuration, Localization, Transfer, new ThemeService(() => true)) { Dialogs = Dialogs };

	public string PathOf(string fileName) => Path.Combine(Directory, fileName);

	public void Dispose()
	{
		try
		{
			System.IO.Directory.Delete(Directory, recursive: true);
		}
		catch (IOException)
		{
			// best effort
		}
	}
}

public static class SettingsViewModelExtensions
{
	public static IEnumerable<SettingViewModel> AllSettings(this SettingsViewModel viewModel) =>
		viewModel.Categories.SelectMany(category => category.Settings);

	public static T Setting<T>(this SettingsViewModel viewModel, string title) where T : SettingViewModel =>
		viewModel.AllSettings().OfType<T>().Single(setting => setting.Title == title);

	public static SettingsCategoryViewModel Category(this SettingsViewModel viewModel, string id) =>
		viewModel.Categories.Single(category => category.Id == id);
}
