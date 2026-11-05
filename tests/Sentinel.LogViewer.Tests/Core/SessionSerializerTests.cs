using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Core.Sessions;
using Xunit;

namespace Sentinel.LogViewer.Tests.Core;

/// <summary>
/// Tests of the session file: what is written is what is read, and what is no session is told
/// </summary>
public class SessionSerializerTests
{
	private static readonly DateTime Start = new(2026, 5, 15, 10, 0, 0, DateTimeKind.Utc);

	private static LogEntry Entry(string message, int minute) => new(LogEntryLevel.Warn, "Acme.Shop", message)
	{
		TimeStamp = Start.AddMinutes(minute),
		Exception = "System.Exception: boom",
		Application = "Acme",
		Sender = "PC1 (10.0.0.5)",
		Id = 40 + minute,
		Properties = new Dictionary<string, object?> { ["Thread"] = "worker 4", ["LineNumber"] = 42, ["Ratio"] = 0.5, ["Done"] = true, ["Nothing"] = null }
	};

	private static SessionData RoundTrip(SessionData data)
	{
		using var stream = new MemoryStream();
		SessionSerializer.Write(stream, data);
		stream.Position = 0;
		return SessionSerializer.Read(stream);
	}

	[Fact]
	public void ATab_KeepsItsEntriesFiltersColumnsAndBookmarks()
	{
		var entries = new[] { Entry("first", 1), Entry("second", 2) };
		var tab = new SessionTab
		{
			Header = "Acme (PC1)",
			SenderKey = "acme|pc1",
			ColourIndex = 3,
			MaxCount = 5000,
			RestartBehavior = "NewTab",
			Columns = new List<string> { "level", "message" },
			FileTimeZone = "UTC",
			Filters = new LogViewerFilterState
			{
				HiddenLevels = { "Trace" },
				Terms = { new LogViewerFilterTerm { Text = "boom", IsRegex = true, Level = LogEntryLevel.Error } },
				LoggerRules = { "-Acme.Noise" },
				TimeStampFormat = TimeStampFormat.Iso,
				TimeFrom = Start
			},
			Entries = entries.Select(SessionEntry.From),
			Bookmarks = { new SessionBookmark { Index = 1, Comment = "look here" } }
		};

		var read = RoundTrip(new SessionData { SelectedTab = 0, Tabs = { tab } });

		var back = Assert.Single(read.Tabs);
		Assert.Equal("Acme (PC1)", back.Header);
		Assert.Equal("acme|pc1", back.SenderKey);
		Assert.Equal(3, back.ColourIndex);
		Assert.Equal(new[] { "level", "message" }, back.Columns);
		Assert.Equal("UTC", back.FileTimeZone);
		Assert.Null(back.DisplayTimeZone);
		Assert.Equal(new[] { "Trace" }, back.Filters!.HiddenLevels);
		Assert.Equal(LogEntryLevel.Error, Assert.Single(back.Filters.Terms).Level);
		Assert.Equal(TimeStampFormat.Iso, back.Filters.TimeStampFormat);
		Assert.Equal(Start, back.Filters.TimeFrom);
		Assert.Equal(1, Assert.Single(back.Bookmarks).Index);
		Assert.Equal("look here", back.Bookmarks[0].Comment);

		var second = back.Entries.Select(entry => entry.ToLogEntry()).ToList()[1];
		Assert.Equal(LogEntryLevel.Warn, second.Level);
		Assert.Equal("Acme.Shop", second.LoggerName);
		Assert.Equal("second", second.Message);
		Assert.Equal(Start.AddMinutes(2), second.TimeStamp);
		Assert.Equal(DateTimeKind.Utc, second.TimeStamp.Kind);
		Assert.Equal("System.Exception: boom", second.Exception);
		Assert.Equal("Acme", second.Application);
		Assert.Equal("PC1 (10.0.0.5)", second.Sender);
		Assert.Equal(42, second.Id);
	}

	[Fact]
	public void TheProperties_ComeBackAsTextNumbersAndSwitches()
	{
		var read = RoundTrip(new SessionData { Tabs = { new SessionTab { Entries = new[] { SessionEntry.From(Entry("x", 1)) } } } });

		var properties = read.Tabs[0].Entries.Single().ToLogEntry().Properties;
		Assert.Equal("worker 4", properties["Thread"]);
		Assert.Equal(42L, properties["LineNumber"]);
		Assert.Equal(0.5, properties["Ratio"]);
		Assert.Equal(true, properties["Done"]);
		Assert.Null(properties["Nothing"]);
	}

	[Fact]
	public void AnEntryWithoutPropertiesApplicationOrSender_ComesBackWithEmptyOnes()
	{
		var read = RoundTrip(new SessionData { Tabs = { new SessionTab { Entries = new[] { SessionEntry.From(new LogEntry(LogEntryLevel.Info, "A", "m")) } } } });

		var entry = read.Tabs[0].Entries.Single().ToLogEntry();
		Assert.Empty(entry.Properties);
		Assert.Equal(string.Empty, entry.Application);
		Assert.Equal(string.Empty, entry.Sender);
		Assert.Null(entry.Exception);
	}

	[Fact]
	public void ASessionOfANewerVersion_IsRefused()
	{
		var exception = Assert.Throws<InvalidDataException>(() => RoundTrip(new SessionData { Version = SessionData.CurrentVersion + 1 }));

		Assert.Contains("newer version", exception.Message);
	}

	[Theory]
	[InlineData("")]
	[InlineData("plain text, not compressed")]
	public void AFileThatIsNoSession_IsRefused(string content)
	{
		using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

		Assert.Throws<InvalidDataException>(() => SessionSerializer.Read(stream));
	}

	[Fact]
	public void Save_ReplacesTheFileWhole_AndLeavesNoTemporaryFile()
	{
		var directory = Directory.CreateTempSubdirectory("session-test").FullName;
		try
		{
			var path = Path.Combine(directory, "case" + SessionSerializer.Extension);
			SessionSerializer.Save(path, new SessionData { Tabs = { new SessionTab { Header = "old" } } });
			SessionSerializer.Save(path, new SessionData { Tabs = { new SessionTab { Header = "new" } } });

			Assert.Equal("new", SessionSerializer.Load(path).Tabs.Single().Header);
			Assert.Equal(new[] { path }, Directory.GetFiles(directory));
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}
}
