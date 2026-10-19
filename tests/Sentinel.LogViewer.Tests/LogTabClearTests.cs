using System.Linq;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Models;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests that the number in the header of a tab follows the Clear button of the log viewer
/// </summary>
[Collection(WpfCollection.Name)]
public class LogTabClearTests
{
	private readonly WpfStaContextFixture _context;

	public LogTabClearTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	[Fact]
	public void ClearOfTheViewer_ResetsTheNumberOfTheTab()
	{
		_context.RunOnSta(() =>
		{
			var tab = new LogTabViewModel();
			foreach (var number in Enumerable.Range(0, 24))
				tab.AddLogEntry(new LogEntry(LogEntryLevel.Info, "Logger", "message " + number));
			var (window, viewer) = WpfTestHelper.ShowInWindow(tab.LogEntries);
			try
			{
				Assert.Equal(24, tab.LogCount);

				viewer.ClearCommand.Execute(null);

				Assert.Empty(tab.LogEntries);
				Assert.Equal(0, tab.LogCount);
			}
			finally
			{
				window.Close();
			}
		});
	}
}
