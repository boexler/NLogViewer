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
	public void Import_DropsValuesThatWouldBreakTheApplication()
	{
		var file = _environment.PathOf("edited.json");
		File.WriteAllText(file, """
			{
			  "Version": 1,
			  "Settings": {
			    "Ports": [ "udp://0.0.0.0:4000", "not a port", "tcp://0.0.0.0:4505" ],
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
	[InlineData("tcp://0.0.0.0:4000", false, 0)]
	[InlineData("0.0.0.0:4000", false, 0)]
	[InlineData("", false, 0)]
	[InlineData(null, false, 0)]
	public void TryGetUdpPort_AcceptsOnlyUdpAddressesWithAPort(string? address, bool expected, int expectedPort)
	{
		Assert.Equal(expected, SettingsRules.TryGetUdpPort(address, out var port));
		Assert.Equal(expectedPort, port);
	}
}
