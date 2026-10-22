using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Services;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// The words of a notification in the tray
/// </summary>
public class TrayNotificationTextTests
{
	private const string More = "(+{0} more)";

	private static LogEntry Entry(string message, string logger = "Acme.Billing", LogEntryLevel level = LogEntryLevel.Error) => new(level, logger, message);

	[Fact]
	public void TheTitleNamesTheApplicationAndTheLevel_TheTextTheLoggerAndTheMessage()
	{
		var (title, text) = TrayNotificationText.Build("MyApp", Entry("Payment failed"), 1, More);

		Assert.Equal("MyApp · Error", title);
		Assert.Equal("Acme.Billing: Payment failed", text);
	}

	[Fact]
	public void WithoutALogger_TheTextIsTheMessage_AndLinesBecomeOneLine()
	{
		var (_, text) = TrayNotificationText.Build("MyApp", Entry("Payment failed\r\n   at Billing.Charge()\n  at Program.Main()", logger: ""), 1, More);

		Assert.Equal("Payment failed at Billing.Charge() at Program.Main()", text);
	}

	[Fact]
	public void SeveralEntries_NameHowManyMoreCame_AtTheEndOfTheText()
	{
		var (_, text) = TrayNotificationText.Build("MyApp", Entry("Payment failed"), 5, More);

		Assert.Equal("Acme.Billing: Payment failed (+4 more)", text);
	}

	[Fact]
	public void LongTexts_AreCutWhereWindowsCutsThem_ButTheNumberOfTheOthersStays()
	{
		var (title, text) = TrayNotificationText.Build(new string('A', 100), Entry(new string('m', 500)), 12, More);

		Assert.Equal(TrayNotificationText.MaxTitleLength, title.Length);
		Assert.EndsWith("…" + " · Error", title);
		Assert.Equal(TrayNotificationText.MaxTextLength, text.Length);
		Assert.EndsWith("…" + " (+11 more)", text);
	}

	[Fact]
	public void WithoutAnApplicationName_TheTitleIsTheLevel()
	{
		var (title, _) = TrayNotificationText.Build(string.Empty, Entry("Payment failed"), 1, More);

		Assert.Equal("Error", title);
	}

	[Fact]
	public void ACutNeverLeavesHalfOfAnEmoji()
	{
		// The emoji is two chars and the cut would fall between them.
		var message = new string('m', TrayNotificationText.MaxTextLength - 2) + "\U0001F600" + "tail";

		var (_, text) = TrayNotificationText.Build("MyApp", Entry(message, logger: ""), 1, More);

		Assert.DoesNotContain(text, char.IsSurrogate);
		Assert.EndsWith("…", text);
		Assert.True(text.Length <= TrayNotificationText.MaxTextLength);
	}

	[Fact]
	public void ATextThatFits_IsNotCut()
	{
		var message = new string('m', 200);

		var (_, text) = TrayNotificationText.Build("MyApp", Entry(message, logger: ""), 1, More);

		Assert.Equal(message, text);
	}
}
