using Microsoft.Extensions.Logging;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Extensions.Logging;
using Sentinel.LogViewer.TestApp;
using Sentinel.LogViewer.TestApp.ControlTest;
using Sentinel.LogViewer.TestApp.Embedding;
using Sentinel.LogViewer.TestApp.Generator;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// Tests that the windows of the test application can be made (their XAML is valid and the controls in it are made). They are not shown.
/// </summary>
[Collection(WpfCollection.Name)]
public class TestApplicationWindowsTests
{
	private readonly WpfStaContextFixture _context;

	public TestApplicationWindowsTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	[Fact]
	public void TheStartWindow_TheGenerator_AndTheMessageWindow_CanBeMade() =>
		_context.RunOnSta(() =>
		{
			var settings = new GeneratorSettings(GeneratorLibrary.PlainXml, "udp", "127.0.0.1", 4000);

			Assert.Null(new StartWindow().Generator);
			Assert.Contains("udp://127.0.0.1:4000", new GeneratorWindow(settings).Title);
			Assert.Contains("udp://127.0.0.1:4000", new MessageWindow(settings).Title);
		});

	[Fact]
	public void TheControlTest_CanBeMade_ForTheThreeWaysToConnectTheLogging() =>
		_context.RunOnSta(() =>
		{
			var before = LogBuffer.Default;
			try
			{
				// The mode with NLog writes a file; the other two need nothing from the machine.
				foreach (var mode in new[] { EmbeddingMode.NoDi, EmbeddingMode.HostWithDi, EmbeddingMode.HostWithNLog })
				{
					using var embedding = EmbeddingModes.Start(mode);
					var window = new ControlTestWindow(mode, embedding.Factory);
					window.Close();

					embedding.Factory.CreateLogger("Test").LogInformation("arrives in the default buffer");
					Assert.True(LogBuffer.Default.Count > 0);
				}
			}
			finally
			{
				LogBuffer.Default = before;
			}
		});
}
