using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Wpf.Models;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// Tests for <see cref="TimeStampFormats"/>: the format as it is saved in the configuration
/// </summary>
public class TimeStampFormatsTests
{
	[Theory]
	[InlineData(TimeStampFormat.System, "system")]
	[InlineData(TimeStampFormat.Iso, "iso")]
	[InlineData(TimeStampFormat.German, "german")]
	[InlineData(TimeStampFormat.English, "english")]
	public void ToIdAndParse_RoundTrip(TimeStampFormat format, string id)
	{
		Assert.Equal(id, TimeStampFormats.ToId(format));
		Assert.Equal(format, TimeStampFormats.Parse(id));
	}

	[Theory]
	[InlineData("ISO", TimeStampFormat.Iso)]
	[InlineData("German", TimeStampFormat.German)]
	[InlineData("nonsense", TimeStampFormat.System)]
	[InlineData("42", TimeStampFormat.System)]
	[InlineData("", TimeStampFormat.System)]
	[InlineData(null, TimeStampFormat.System)]
	public void Parse_IgnoresCase_AndFallsBackToTheSystemFormat(string? id, TimeStampFormat expected)
	{
		Assert.Equal(expected, TimeStampFormats.Parse(id));
	}

	[Fact]
	public void TheFormat_IsPartOfTheSettingsThatAreExportedAndImported()
	{
		using var environment = new Sentinel.LogViewer.Tests.ViewModels.SettingsTestEnvironment();
		var configuration = environment.Saved;
		configuration.TimeStampFormat = "english";
		environment.Configuration.SaveConfiguration(configuration);
		var file = environment.PathOf("export.json");
		environment.Transfer.Export(file);
		configuration.TimeStampFormat = "iso";
		environment.Configuration.SaveConfiguration(configuration);

		environment.Transfer.Import(file);

		Assert.Equal("english", environment.Saved.TimeStampFormat);
	}
}
