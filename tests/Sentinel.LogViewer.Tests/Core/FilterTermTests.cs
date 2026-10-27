using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sentinel.LogViewer.Core;
using Xunit;

namespace Sentinel.LogViewer.Tests.Core;

/// <summary>
/// Tests for <see cref="FilterTerm"/>: whether an entry matches, with every option and every field, and where a text is found
/// </summary>
public class FilterTermTests
{
	private static LogEntry Entry(string message, string logger = "Acme.Shop", LogEntryLevel level = LogEntryLevel.Info, string? exception = null,
		params (string Name, object Value)[] properties) => new(level, logger, message)
	{
		Exception = exception,
		Properties = properties.ToDictionary(property => property.Name, property => (object?)property.Value)
	};

	[Theory]
	[InlineData("timeout", "Connection TIMEOUT after 30 s", true)]
	[InlineData("timeout", "all fine", false)]
	[InlineData("", "all fine", false)]
	public void ByDefault_TheTextIsFoundWithoutRegardToCase(string text, string message, bool expected)
	{
		Assert.Equal(expected, new FilterTerm(text).Matches(Entry(message)));
	}

	[Fact]
	public void WithMatchCase_UpperAndLowerCaseAreDifferent()
	{
		var term = new FilterTerm("Timeout", matchCase: true);

		Assert.True(term.Matches(Entry("a Timeout happened")));
		Assert.False(term.Matches(Entry("a timeout happened")));
	}

	[Theory]
	[InlineData("order", "the order was created", true)]
	[InlineData("order", "reorder the items", false)]
	[InlineData("order", "orders are shown", false)]
	[InlineData("order", "(order)", true)]
	[InlineData("order", "order_id=5", false)]
	[InlineData("C#", "written in C# and more", true)]
	[InlineData("C#", "written in C#sharp", false)]
	public void WithWholeWord_TheTextCountsOnlyWhenItIsNotPartOfALongerWord(string text, string message, bool expected)
	{
		Assert.Equal(expected, new FilterTerm(text, wholeWord: true).Matches(Entry(message)));
	}

	[Fact]
	public void ARegexMatchesAPattern_AndTheSpecialCharactersOfAPlainTextAreLiteral()
	{
		Assert.True(new FilterTerm(@"HTTP 5\d\d", isRegex: true).Matches(Entry("answered with HTTP 503")));
		Assert.False(new FilterTerm(@"HTTP 5\d\d", isRegex: true).Matches(Entry("answered with HTTP 404")));

		Assert.True(new FilterTerm("a.b").Matches(Entry("the value a.b is set")));
		Assert.False(new FilterTerm("a.b").Matches(Entry("the value axb is set")));
	}

	[Fact]
	public void ARegex_CanBeCombinedWithMatchCaseAndWholeWord()
	{
		Assert.True(new FilterTerm("error|fail", isRegex: true).Matches(Entry("it did FAIL")));
		Assert.False(new FilterTerm("error|fail", isRegex: true, matchCase: true).Matches(Entry("it did FAIL")));
		Assert.False(new FilterTerm("error|fail", isRegex: true, wholeWord: true).Matches(Entry("failure")));
		Assert.True(new FilterTerm("error|fail", isRegex: true, wholeWord: true).Matches(Entry("did fail twice")));
	}

	[Fact]
	public void AnInvalidRegex_IsRejected_AndSaysWhy()
	{
		Assert.Throws<ArgumentException>(() => new FilterTerm("(unclosed", isRegex: true));

		Assert.NotNull(FilterTerm.Validate("(unclosed", isRegex: true));
		Assert.Null(FilterTerm.Validate("(closed)", isRegex: true));
		Assert.Null(FilterTerm.Validate("(unclosed", isRegex: false));
		Assert.Null(FilterTerm.Validate("", isRegex: true));
	}

	[Fact]
	public void ARegexThatTakesTooLong_DoesNotMatch_InsteadOfBlockingTheViewer()
	{
		var term = new FilterTerm("^(a+)+$", isRegex: true);
		var entry = Entry(new string('a', 60) + "b");

		var watch = Stopwatch.StartNew();
		var matched = term.Matches(entry);

		Assert.False(matched);
		Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
	}

	[Fact]
	public void TheLevel_LimitsTheTermToEntriesOfThatLevel()
	{
		var term = new FilterTerm("timeout", level: LogEntryLevel.Error);

		Assert.True(term.Matches(Entry("timeout", level: LogEntryLevel.Error)));
		Assert.False(term.Matches(Entry("timeout", level: LogEntryLevel.Warn)));
	}

	[Fact]
	public void ByDefault_OnlyTheMessageAndTheLoggerAreSearched()
	{
		var entry = Entry("plain message", "Acme.Shop.Orders", exception: "System.TimeoutException: boom",
			properties: new[] { ("Thread", (object)"worker-7"), ("ClassName", "OrderService") });

		Assert.True(new FilterTerm("plain").Matches(entry));
		Assert.True(new FilterTerm("Orders").Matches(entry));
		Assert.False(new FilterTerm("TimeoutException").Matches(entry));
		Assert.False(new FilterTerm("worker-7").Matches(entry));
		Assert.False(new FilterTerm("OrderService").Matches(entry));
	}

	[Fact]
	public void TheFields_DecideWhereTheTextIsLookedFor()
	{
		var entry = Entry("plain message", "Acme.Shop.Orders", exception: "System.TimeoutException: boom",
			properties: new[] { ("Thread", (object)"worker-7"), ("ClassName", "OrderService"), ("LineNumber", 42) });

		Assert.True(new FilterTerm("TimeoutException").Matches(entry, SearchFields.Exception));
		Assert.False(new FilterTerm("TimeoutException").Matches(entry, SearchFields.Message | SearchFields.LoggerName));

		Assert.True(new FilterTerm("worker-7").Matches(entry, SearchFields.Thread));
		Assert.False(new FilterTerm("worker-7").Matches(entry, SearchFields.Properties));

		Assert.True(new FilterTerm("OrderService").Matches(entry, SearchFields.Properties));
		Assert.True(new FilterTerm("42").Matches(entry, SearchFields.Properties));
		Assert.False(new FilterTerm("OrderService").Matches(entry, SearchFields.Thread));

		Assert.True(new FilterTerm("worker-7").Matches(entry, SearchFields.All));
		Assert.False(new FilterTerm("plain").Matches(entry, SearchFields.None));
	}

	[Fact]
	public void TheExcludeFlag_IsOnlyAnAttributeOfTheTerm_MatchesStillSaysWhetherTheTextIsThere()
	{
		var term = new FilterTerm("retry", isExclude: true);

		Assert.True(term.IsExclude);
		Assert.True(term.Matches(Entry("will retry soon")));
		Assert.False(term.Matches(Entry("done")));
	}

	[Fact]
	public void FindMatches_ReturnsEveryPlaceOfTheText()
	{
		Assert.Equal(new[] { (0, 3), (8, 3) }, new FilterTerm("abc").FindMatches("ABC and abc").ToArray());
		Assert.Equal(new[] { (8, 3) }, new FilterTerm("abc", matchCase: true).FindMatches("ABC and abc").ToArray());
		Assert.Equal(new[] { (4, 5) }, new FilterTerm("order", wholeWord: true).FindMatches("the order, reorder, orders").ToArray());
		Assert.Equal(new[] { (4, 3), (10, 3) }, new FilterTerm(@"\d+", isRegex: true).FindMatches("abc 123 x 456").ToArray());
		Assert.Empty(new FilterTerm("zzz").FindMatches("nothing"));
		Assert.Empty(new FilterTerm("").FindMatches("nothing"));
		Assert.Empty(new FilterTerm("x*", isRegex: true).FindMatches("abc"));
	}

	[Fact]
	public void IsSameAs_ComparesTheTextAndEveryOption()
	{
		var term = new FilterTerm("a", wholeWord: true);

		Assert.True(term.IsSameAs(new FilterTerm("a", wholeWord: true)));
		Assert.False(term.IsSameAs(new FilterTerm("a")));
		Assert.False(term.IsSameAs(new FilterTerm("a", wholeWord: true, isExclude: true)));
		Assert.False(term.IsSameAs(new FilterTerm("a", wholeWord: true, level: LogEntryLevel.Warn)));
		Assert.False(term.IsSameAs(null));
	}

	[Fact]
	public void ATerm_IsOnByDefault_AndTellsWhenItIsSwitched()
	{
		var term = new FilterTerm("a");
		var changed = new List<string?>();
		term.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		Assert.True(term.IsEnabled);
		term.IsEnabled = true;
		term.IsEnabled = false;

		Assert.False(term.IsEnabled);
		Assert.Equal(new[] { nameof(FilterTerm.IsEnabled) }, changed);
		Assert.True(term.Matches(new LogEntry(LogEntryLevel.Info, "A", "a")), "a term that is off still says what it matches; whoever applies the terms skips it");
	}}
