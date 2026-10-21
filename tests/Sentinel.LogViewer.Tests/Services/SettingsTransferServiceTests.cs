using Sentinel.LogViewer.Core;
using System;
using System.IO;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Tests.ViewModels;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// Tests for <see cref="SettingsTransferService"/> and the rules shared with the settings window
/// </summary>
public class SettingsTransferServiceTests : IDisposable
{
	private readonly SettingsTestEnvironment _environment = new();

	public void Dispose() => _environment.Dispose();

	[Fact]
	public void Import_ReplacesSettingsAndColumnMappings()
	{
		var file = _environment.PathOf("export.json");
		var configuration = _environment.Saved;
		configuration.MaxLogEntriesPerTab = 20000;
		configuration.Ports = new() { "udp://0.0.0.0:4010" };
		configuration.IncludePrereleaseUpdates = true;
		_environment.Configuration.SaveConfiguration(configuration);
		_environment.Formats.SaveFormatForPattern("a.log", new TextFileFormat { Separator = ";" });
		_environment.Transfer.Export(file);

		_environment.Configuration.SaveConfiguration(ConfigurationService.CreateDefaultConfiguration());
		_environment.Formats.ReplaceAll(new TextFileFormatConfigCollection());
		_environment.Transfer.Import(file);

		var saved = _environment.Saved;
		Assert.Equal(20000, saved.MaxLogEntriesPerTab);
		Assert.Equal(new[] { "udp://0.0.0.0:4010" }, saved.Ports);
		Assert.True(saved.IncludePrereleaseUpdates);
		Assert.Equal(";", Assert.Single(_environment.Formats.GetAll().Formats).Format.Separator);
	}

	[Fact]
	public void TheVersionLastSeen_StaysOnThisComputer_NotExportedAndNotReplacedByAnImport()
	{
		var file = _environment.PathOf("export.json");
		var configuration = _environment.Saved;
		configuration.LastSeenVersion = "5.0.0-beta.2";
		configuration.ShowChangelogAfterUpdate = false;
		_environment.Configuration.SaveConfiguration(configuration);
		_environment.Transfer.Export(file);

		Assert.DoesNotContain("5.0.0-beta.2", File.ReadAllText(file));

		configuration.LastSeenVersion = "5.0.0-beta.3";
		configuration.ShowChangelogAfterUpdate = true;
		_environment.Configuration.SaveConfiguration(configuration);
		_environment.Transfer.Import(file);

		var saved = _environment.Saved;
		Assert.Equal("5.0.0-beta.3", saved.LastSeenVersion);
		Assert.False(saved.ShowChangelogAfterUpdate);
	}

	[Fact]
	public void TheChoicesAndFiltersOfApplications_AreExportedAndImported_AndInvalidValuesAreDropped()
	{
		var file = _environment.PathOf("export.json");
		var configuration = _environment.Saved;
		configuration.RestartBehavior = "ExtendTab";
		configuration.Applications["myapp;10.0.0.5"] = new ApplicationTabSettings
		{
			TakeOverFilters = true,
			RestartBehavior = "ReplaceTab",
			Filters = new Sentinel.LogViewer.Core.LogViewerFilterState { HiddenLevels = { "Trace" } }
		};
		_environment.Configuration.SaveConfiguration(configuration);
		_environment.Transfer.Export(file);

		_environment.Configuration.SaveConfiguration(ConfigurationService.CreateDefaultConfiguration());
		_environment.Transfer.Import(file);

		var saved = _environment.Saved;
		Assert.Equal("ExtendTab", saved.RestartBehavior);
		var application = saved.Applications["myapp;10.0.0.5"];
		Assert.Equal("ReplaceTab", application.RestartBehavior);
		Assert.Equal(new[] { "Trace" }, application.Filters?.HiddenLevels);

		File.WriteAllText(file, """{ "Version": 1, "Settings": { "RestartBehavior": "sometimes" } }""");
		_environment.Transfer.Import(file);
		Assert.Equal("NewTab", _environment.Saved.RestartBehavior);
	}

	[Fact]
	public void TheLoggerTreeSettings_AreExportedAndImported_AndAnInvalidWidthBecomesTheDefault()
	{
		var file = _environment.PathOf("export.json");
		var configuration = _environment.Saved;
		configuration.LoggerTreeOnLeft = true;
		configuration.LoggerTreeOpen = true;
		configuration.LoggerTreeWidth = 410;
		_environment.Configuration.SaveConfiguration(configuration);
		_environment.Transfer.Export(file);

		_environment.Configuration.SaveConfiguration(ConfigurationService.CreateDefaultConfiguration());
		_environment.Transfer.Import(file);
		var saved = _environment.Saved;
		Assert.True(saved.LoggerTreeOnLeft);
		Assert.True(saved.LoggerTreeOpen);
		Assert.Equal(410, saved.LoggerTreeWidth);

		File.WriteAllText(file, """{ "Version": 1, "Settings": { "LoggerTreeWidth": 7 } }""");
		_environment.Transfer.Import(file);
		Assert.Equal(260, _environment.Saved.LoggerTreeWidth);
	}

	[Fact]
	public void TheSearchFields_AreExportedAndImported_AndInvalidOnesBecomeTheDefault()
	{
		var file = _environment.PathOf("export.json");
		var configuration = _environment.Saved;
		configuration.SearchFields = (SearchFields.Message | SearchFields.Exception).ToString();
		_environment.Configuration.SaveConfiguration(configuration);
		_environment.Transfer.Export(file);

		_environment.Configuration.SaveConfiguration(ConfigurationService.CreateDefaultConfiguration());
		_environment.Transfer.Import(file);
		Assert.Equal(SearchFields.Message | SearchFields.Exception, SettingsRules.ParseSearchFields(_environment.Saved.SearchFields));

		File.WriteAllText(file, """{ "Version": 1, "Settings": { "SearchFields": "Nonsense" } }""");
		_environment.Transfer.Import(file);
		Assert.Equal("Default", _environment.Saved.SearchFields);
	}

	[Fact]
	public void Import_DropsValuesThatWouldBreakTheApplication()
	{
		var file = _environment.PathOf("edited.json");
		File.WriteAllText(file, """
			{
			  "Version": 1,
			  "Settings": {
			    "Ports": [ "udp://0.0.0.0:4000", "not a port", "http://0.0.0.0:4505" ],
			    "Language": "xx",
			    "MaxLogEntriesPerTab": 5
			  }
			}
			""");

		_environment.Transfer.Import(file);

		var saved = _environment.Saved;
		Assert.Equal(new[] { "udp://0.0.0.0:4000" }, saved.Ports);
		Assert.Equal(string.Empty, saved.Language);
		Assert.Equal(10000, saved.MaxLogEntriesPerTab);
	}

	[Fact]
	public void Import_KeepsTheThemeAndItsColours_AndDropsWhatDoesNotExist()
	{
		var file = _environment.PathOf("themed.json");
		File.WriteAllText(file, """
			{
			  "Version": 1,
			  "Settings": {
			    "ThemeMode": "dark",
			    "Theme": "oldschool",
			    "ThemeColors": {
			      "oldschool": { "ThemeAccent": "#abc", "ThemeBorder": "nonsense", "NoSuchToken": "#000000" },
			      "gone": { "ThemeAccent": "#000000" }
			    }
			  }
			}
			""");

		_environment.Transfer.Import(file);

		var saved = _environment.Saved;
		Assert.Equal("dark", saved.ThemeMode);
		Assert.Equal("oldschool", saved.Theme);
		var colors = Assert.Single(saved.ThemeColors);
		Assert.Equal("oldschool", colors.Key);
		Assert.Equal(new System.Collections.Generic.Dictionary<string, string> { ["ThemeAccent"] = "#AABBCC" }, colors.Value);
	}

	[Theory]
	[InlineData("not json")]
	[InlineData("[]")]
	[InlineData("{ \"Settings\": {} }")]
	[InlineData("{ \"Version\": 1 }")]
	[InlineData("{ \"Version\": 99, \"Settings\": {} }")]
	public void Import_OfSomethingElse_Throws(string content)
	{
		var file = _environment.PathOf("other.json");
		File.WriteAllText(file, content);

		Assert.Throws<InvalidDataException>(() => _environment.Transfer.Import(file));
	}

	[Theory]
	[InlineData("udp://0.0.0.0:4000", true, 4000)]
	[InlineData(" udp://127.0.0.1:1 ", true, 1)]
	[InlineData("udp://localhost:65535", true, 65535)]
	[InlineData("udp://0.0.0.0:0", false, 0)]
	[InlineData("udp://0.0.0.0:65536", false, 0)]
	[InlineData("udp://0.0.0.0", false, 0)]
	[InlineData("tcp://0.0.0.0:4000", true, 4000)]
	[InlineData("http://0.0.0.0:4000", false, 0)]
	[InlineData("0.0.0.0:4000", false, 0)]
	[InlineData("", false, 0)]
	[InlineData(null, false, 0)]
	public void TryGetPort_AcceptsUdpAndTcpAddressesWithAPort(string? address, bool expected, int expectedPort)
	{
		Assert.Equal(expected, SettingsRules.TryGetPort(address, out var port));
		Assert.Equal(expectedPort, port);
	}
}
