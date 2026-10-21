using System.Linq;
using Sentinel.LogViewer.Core;
using Xunit;

namespace Sentinel.LogViewer.Tests.Core;

public class Log4JEventFramerTests
{
	private static string Event(string message) =>
		$@"<log4j:event logger=""L"" level=""INFO"" timestamp=""1"" xmlns:log4j=""http://jakarta.apache.org/log4j/""><log4j:message>{message}</log4j:message></log4j:event>";

	[Fact]
	public void SeveralEvents_InOnePiece_AreCutApart_AndWhatIsBetweenThemIsDropped()
	{
		var events = new Log4JEventFramer().Add(Event("a") + "\r\n" + Event("b") + "junk" + Event("c")).ToList();

		Assert.Equal(new[] { Event("a"), Event("b"), Event("c") }, events);
	}

	[Fact]
	public void AnEvent_ThatArrivesInManyPieces_IsReturnedWhenItIsComplete()
	{
		var framer = new Log4JEventFramer();
		var text = Event("x") + Event("y");
		var result = text.Chunk(7).SelectMany(piece => framer.Add(new string(piece))).ToList();

		Assert.Equal(new[] { Event("x"), Event("y") }, result);
	}

	[Fact]
	public void TheStartTag_CutInTwo_IsNotLost()
	{
		var framer = new Log4JEventFramer();
		var text = Event("a");

		Assert.Empty(framer.Add(text[..9]));
		Assert.Equal(new[] { text }, framer.Add(text[9..]));
	}

	[Fact]
	public void AnEventThatNeverEnds_IsDroppedAtALimit()
	{
		var framer = new Log4JEventFramer();
		framer.Add("<log4j:event " + new string('x', 5 * 1024 * 1024));

		Assert.Equal(new[] { Event("ok") }, framer.Add(Event("ok")));
	}
}
