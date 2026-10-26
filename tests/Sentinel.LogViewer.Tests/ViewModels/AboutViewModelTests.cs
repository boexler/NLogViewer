using System;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Updates;
using Sentinel.LogViewer.ViewModels;
using Xunit;
using static Sentinel.LogViewer.Tests.WpfTestHelper;

namespace Sentinel.LogViewer.Tests.ViewModels;

[Collection(WpfCollection.Name)]
public class AboutViewModelTests
{
	private readonly WpfStaContextFixture _context;

	public AboutViewModelTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
			Task.FromResult(respond(request));
	}

	private const string Feed = """
		<feed xmlns="http://www.w3.org/2005/Atom"><entry><link rel="alternate" href="https://github.com/boexler/NLogViewer/releases/tag/v99.0.0"/></entry></feed>
		""";

	[Fact]
	public async Task WhenTheApiLimitIsReachedForTheDownload_TheDialogSaysWhenToTryAgain_AndTheCheckStillWorks()
	{
		using var environment = new SettingsTestEnvironment();
		var updateService = new UpdateService(new Handler(request =>
		{
			if (request.RequestUri!.Host == "github.com")
				return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Feed) };

			var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
			response.Headers.Add("X-RateLimit-Remaining", "0");
			response.Headers.Add("X-RateLimit-Reset", "1900000000");
			return response;
		}), InstallType.Msi);
		var about = new AboutViewModel(updateService, environment.Configuration, environment.Localization,
			new ChangelogService(updateService, environment.PathOf("changelog.json")));

		await about.CheckAsync();
		Assert.Equal(UpdateState.UpdateAvailable, about.State);

		about.DownloadCommand.Execute(null);
		for (var i = 0; i < 100 && about.State is UpdateState.UpdateAvailable or UpdateState.Downloading; i++)
			await Task.Delay(20);

		Assert.Equal(UpdateState.Failed, about.State);
		Assert.StartsWith("GitHub allows only a limited number of requests per hour. Try again after", about.StatusText);
	}
	private static AboutViewModel ChocolateyAbout(SettingsTestEnvironment environment)
	{
		var updateService = new UpdateService(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Feed) }), InstallType.Chocolatey);
		return new AboutViewModel(updateService, environment.Configuration, environment.Localization,
			new ChangelogService(updateService, environment.PathOf("changelog.json")));
	}

	[Fact]
	public async Task CopyingTheCommand_PutsItOnTheClipboard_AndShowsTheCheckMarkForAMoment()
	{
		using var environment = new SettingsTestEnvironment();
		string? copied = null;
		var about = ChocolateyAbout(environment);
		about.SetClipboardText = text => copied = text;
		about.CopiedFor = TimeSpan.FromMilliseconds(50);

		var nameBefore = about.CopyButtonName;

		about.CopyChocolateyCommand.Execute(null);

		Assert.Equal(about.ChocolateyCommand, copied);
		Assert.True(about.CommandCopied);
		Assert.NotEqual(nameBefore, about.CopyButtonName);
		for (var i = 0; i < 100 && about.CommandCopied; i++)
			await Task.Delay(20);
		Assert.False(about.CommandCopied);
		Assert.Equal(nameBefore, about.CopyButtonName);
	}

	[Fact]
	public void WhenAnotherProgramHoldsTheClipboard_CopyingDoesNotThrow_AndShowsNoCheckMark()
	{
		using var environment = new SettingsTestEnvironment();
		var about = ChocolateyAbout(environment);
		about.SetClipboardText = _ => throw new COMException("The clipboard is in use", unchecked((int)0x800401D0));

		about.CopyChocolateyCommand.Execute(null);

		Assert.False(about.CommandCopied);
	}

	[Fact]
	public void TheChocolateyCommand_IsShownInFull_AndTheCopyButtonStaysInTheDialog()
	{
		_context.RunOnSta(() =>
		{
			using var environment = new SettingsTestEnvironment();
			environment.Localization.Initialize();
			environment.Localization.SetLanguage("en");
			var about = ChocolateyAbout(environment);
			// A narrow window, so that the command is longer than the card also with a short version number.
			var window = new AboutWindow(about) { Width = 560, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
			try
			{
				window.Show();
				for (var i = 0; i < 100 && about.State != UpdateState.ManualUpdate; i++)
				{
					Thread.Sleep(50);
					WpfTestHelper.Settle();
				}

				Assert.Equal(UpdateState.ManualUpdate, about.State);
				var box = Descendants<TextBox>(window).Single(candidate => candidate.Text == about.ChocolateyCommand);
				var button = Descendants<Button>(window).Single(candidate => candidate.Command == about.CopyChocolateyCommand);

				Assert.True(box.ExtentWidth <= box.ViewportWidth, "the command runs past the edge of its box");
				var right = button.TranslatePoint(new Point(button.ActualWidth, 0), window).X;
				Assert.True(right <= window.ActualWidth, "the copy button is pushed out of the dialog");
			}
			finally
			{
				window.Close();
			}
		});
	}
}
