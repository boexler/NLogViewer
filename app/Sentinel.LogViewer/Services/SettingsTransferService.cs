using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Services.Theming;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// All settings of the application in one file, to move them to another computer
/// </summary>
public sealed class SettingsBundle
{
	public int Version { get; set; } = SettingsTransferService.CurrentVersion;
	public AppConfiguration Settings { get; set; } = new();
	public TextFileFormatConfigCollection TextFileFormats { get; set; } = new();
}

/// <summary>
/// Exports and imports the settings together with the saved column mappings of text files
/// </summary>
public class SettingsTransferService(ConfigurationService configurationService, TextFileFormatConfigService formatConfigService)
{
	public const int CurrentVersion = 1;

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
	};

	public void Export(string path)
	{
		var settings = configurationService.LoadConfiguration();
		settings.LastSeenVersion = string.Empty;
		var bundle = new SettingsBundle
		{
			Settings = settings,
			TextFileFormats = formatConfigService.GetAll()
		};
		File.WriteAllText(path, JsonSerializer.Serialize(bundle, JsonOptions));
	}

	/// <summary>
	/// Replaces the current settings and column mappings with the content of an exported file
	/// </summary>
	/// <exception cref="InvalidDataException">The file is not a settings export of this application.</exception>
	public void Import(string path)
	{
		var bundle = Read(path);
		var settings = bundle.Settings;

		// Values from a hand-edited file must not break the application.
		settings.Ports = settings.Ports.Where(address => SettingsRules.TryGetUdpPort(address, out _)).ToList();
		if (!SettingsRules.IsValidEntriesPerTab(settings.MaxLogEntriesPerTab))
			settings.MaxLogEntriesPerTab = ConfigurationService.CreateDefaultConfiguration().MaxLogEntriesPerTab;
		if (!string.IsNullOrEmpty(settings.Language) && !LocalizationService.AvailableLanguages.ContainsKey(settings.Language))
			settings.Language = string.Empty;
		ThemeCatalog.Sanitize(settings);

		// The version that ran last belongs to this computer, not to the file.
		settings.LastSeenVersion = configurationService.LoadConfiguration().LastSeenVersion;
		configurationService.SaveConfiguration(settings);
		formatConfigService.ReplaceAll(bundle.TextFileFormats);
	}

	private static SettingsBundle Read(string path)
	{
		var json = File.ReadAllText(path);
		try
		{
			using var document = JsonDocument.Parse(json);
			if (document.RootElement.ValueKind != JsonValueKind.Object ||
			    !document.RootElement.TryGetProperty(nameof(SettingsBundle.Version), out var version) ||
			    !document.RootElement.TryGetProperty(nameof(SettingsBundle.Settings), out _))
			{
				throw new InvalidDataException("The file is not a settings export.");
			}

			if (version.ValueKind != JsonValueKind.Number || version.GetInt32() is < 1 or > CurrentVersion)
				throw new InvalidDataException("The settings export has an unsupported version.");

			var bundle = JsonSerializer.Deserialize<SettingsBundle>(json, JsonOptions)
			             ?? throw new InvalidDataException("The file is not a settings export.");
			bundle.Settings ??= new AppConfiguration();
			bundle.Settings.Ports ??= new();
			bundle.TextFileFormats ??= new();
			bundle.TextFileFormats.Formats ??= new();
			return bundle;
		}
		catch (JsonException ex)
		{
			throw new InvalidDataException("The file is not a settings export.", ex);
		}
	}
}
