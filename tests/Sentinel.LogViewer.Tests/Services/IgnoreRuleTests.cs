using System;
using System.IO;
using System.Linq;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Tests.ViewModels;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// Tests for the senders whose logs are dropped
/// </summary>
public class IgnoreRuleTests : IDisposable
{
	private readonly SettingsTestEnvironment _environment = new();

	public void Dispose() => _environment.Dispose();

	[Theory]
	[InlineData("10.0.0.5", "", "Orders", "10.0.0.5", true)]
	[InlineData("10.0.0.5", "", "Orders", "10.0.0.6", false)]
	[InlineData("", "Orders", "orders", "10.0.0.6", true)]
	[InlineData("", "Orders", "Billing", "10.0.0.5", false)]
	[InlineData("10.0.0.5", "Orders", "Orders", "10.0.0.5", true)]
	[InlineData("10.0.0.5", "Orders", "Orders", "10.0.0.6", false)]
	[InlineData("10.0.0.5", "Orders", "Billing", "10.0.0.5", false)]
	[InlineData("", "", "Orders", "10.0.0.5", false)]
	public void ARule_NeedsEveryPartThatItHas_ToMatch(string ip, string application, string sentBy, string sender, bool expected) =>
		Assert.Equal(expected, new IgnoreRule { Ip = ip, Application = application }.Matches(sentBy, sender));

	[Fact]
	public void WhatAHandEditedFileMakesWrong_IsLeftOut()
	{
		var file = _environment.PathOf("edited.json");
		File.WriteAllText(file, """
			{ "Version": 1, "Settings": { "IgnoreRules": [
				{ "Ip": " 10.0.0.5 ", "Application": "" },
				{ "Ip": "10.0.0.5", "Application": "" },
				{ "Ip": "", "Application": "" },
				{ "Ip": "not an address", "Application": "" },
				{ "Ip": "", "Application": "Orders" },
				null
			] } }
			""");

		_environment.Transfer.Import(file);

		Assert.Equal(new[] { ("10.0.0.5", ""), ("", "Orders") }, _environment.Saved.IgnoreRules.Select(rule => (rule.Ip, rule.Application)));
	}

	[Fact]
	public void TheRules_AreExportedAndImportedWithTheSettings()
	{
		var file = _environment.PathOf("export.json");
		var configuration = _environment.Saved;
		configuration.IgnoreRules.Add(new IgnoreRule { Ip = "10.0.0.5", Application = "Orders" });
		_environment.Configuration.SaveConfiguration(configuration);
		_environment.Transfer.Export(file);

		_environment.Configuration.SaveConfiguration(ConfigurationService.CreateDefaultConfiguration());
		_environment.Transfer.Import(file);

		var rule = Assert.Single(_environment.Saved.IgnoreRules);
		Assert.Equal(("10.0.0.5", "Orders"), (rule.Ip, rule.Application));
	}
}
