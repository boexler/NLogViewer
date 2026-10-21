using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Updates;
using Sentinel.LogViewer.ViewModels;
using Xunit;

namespace Sentinel.LogViewer.Tests.ViewModels;

public class AboutViewModelTests
{
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
}
