using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows.Data;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using Xunit;
using WpfLogViewer = Sentinel.LogViewer.Wpf.LogViewer;

namespace Sentinel.LogViewer.Tests
{
	/// <summary>
	/// Unit tests for LogViewer filter commands (AddRegexSearchTerm and AddRegexSearchTermExclude)
	/// </summary>
	[Collection(WpfCollection.Name)]
	public class LogViewerFilterTests : IDisposable
	{
		private readonly WpfStaContextFixture _context;

		public LogViewerFilterTests(WpfStaContextFixture fixture)
		{
			_context = fixture ?? throw new ArgumentNullException(nameof(fixture));
		}

		[Fact]
		public void AddRegexSearchTerm_AddsAPlainTextTermThatIncludes()
		{
			_context.RunOnSta(() =>
			{
				var viewer = new WpfLogViewer();

				viewer.AddRegexSearchTerm("MyApp.Logger");

				var searchTerm = Assert.Single(viewer.ActiveSearchTerms);
				Assert.Equal("MyApp.Logger", searchTerm.Text);
				Assert.False(searchTerm.IsRegex);
				Assert.False(searchTerm.IsExclude);
			});
		}

		[Fact]
		public void AddRegexSearchTerm_LoggerNameWithDot_MatchesExactLoggerName()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var testData = new ObservableCollection<LogEntry>
				{
					new(LogEntryLevel.Info, "MyApp.Logger", "Test message 1"),
					new(LogEntryLevel.Info, "MyAppLogger", "Test message 2"),
					new(LogEntryLevel.Info, "MyApp.Other", "Test message 3")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTerm("MyApp.Logger");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEntry>().ToList();

				// Should only match "MyApp.Logger", not "MyAppLogger" or "MyApp.Other"
				Assert.Single(visibleItems);
				Assert.Equal("MyApp.Logger", visibleItems[0].LoggerName);
			});
		}

		[Fact]
		public void AddRegexSearchTermExclude_AddsAPlainTextTermThatExcludes()
		{
			_context.RunOnSta(() =>
			{
				var viewer = new WpfLogViewer();

				viewer.AddRegexSearchTermExclude("MyApp.Logger");

				var searchTerm = Assert.Single(viewer.ActiveSearchTerms);
				Assert.Equal("MyApp.Logger", searchTerm.Text);
				Assert.False(searchTerm.IsRegex);
				Assert.True(searchTerm.IsExclude);
			});
		}

		[Fact]
		public void AddRegexSearchTermExclude_ExcludesMatchingEntries()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var testData = new ObservableCollection<LogEntry>
				{
					new(LogEntryLevel.Info, "MyApp.Logger", "Test message 1"),
					new(LogEntryLevel.Info, "OtherLogger", "Test message 2"),
					new(LogEntryLevel.Info, "MyApp.Other", "Test message 3")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTermExclude("MyApp.Logger");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEntry>().ToList();

				// Should exclude "MyApp.Logger" but show others
				Assert.Equal(2, visibleItems.Count);
				Assert.DoesNotContain(visibleItems, item => item.LoggerName == "MyApp.Logger");
				Assert.Contains(visibleItems, item => item.LoggerName == "OtherLogger");
				Assert.Contains(visibleItems, item => item.LoggerName == "MyApp.Other");
			});
		}

		[Fact]
		public void AddRegexSearchTermExclude_ShowsNonMatchingEntries()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var testData = new ObservableCollection<LogEntry>
				{
					new(LogEntryLevel.Info, "MyApp.Logger", "Test message 1"),
					new(LogEntryLevel.Info, "OtherLogger", "Test message 2")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTermExclude("MyApp.Logger");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEntry>().ToList();

				// Should show "OtherLogger" but hide "MyApp.Logger"
				Assert.Single(visibleItems);
				Assert.Equal("OtherLogger", visibleItems[0].LoggerName);
			});
		}

		[Fact]
		public void Filter_IncludeAndExcludeTerms_AppliesBothCorrectly()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var testData = new ObservableCollection<LogEntry>
				{
					new(LogEntryLevel.Info, "MyApp.Logger", "Error occurred"),
					new(LogEntryLevel.Info, "MyApp.Logger", "Info message"),
					new(LogEntryLevel.Info, "OtherLogger", "Error occurred"),
					new(LogEntryLevel.Info, "OtherLogger", "Info message")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTerm("Error"); // Include: must contain "Error"
				viewer.AddRegexSearchTermExclude("MyApp.Logger"); // Exclude: must not contain "MyApp.Logger"

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEntry>().ToList();

				// Should show only "OtherLogger" with "Error" (matches include AND doesn't match exclude)
				Assert.Single(visibleItems);
				Assert.Equal("OtherLogger", visibleItems[0].LoggerName);
				Assert.Equal("Error occurred", visibleItems[0].Message);
			});
		}

		[Fact]
		public void Filter_MultipleIncludeTerms_RequiresAllToMatch()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var testData = new ObservableCollection<LogEntry>
				{
					new(LogEntryLevel.Info, "MyApp.Logger", "Error occurred"),
					new(LogEntryLevel.Info, "MyApp.Logger", "Warning message"),
					new(LogEntryLevel.Info, "OtherLogger", "Error occurred"),
					new(LogEntryLevel.Info, "OtherLogger", "Warning message")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTerm("Error");
				viewer.AddRegexSearchTerm("MyApp");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEntry>().ToList();

				// Should show only entries that match BOTH "Error" AND "MyApp"
				Assert.Single(visibleItems);
				Assert.Equal("MyApp.Logger", visibleItems[0].LoggerName);
				Assert.Equal("Error occurred", visibleItems[0].Message);
			});
		}

		[Fact]
		public void Filter_MultipleExcludeTerms_HidesIfAnyMatches()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var testData = new ObservableCollection<LogEntry>
				{
					new(LogEntryLevel.Info, "MyApp.Logger", "Test message 1"),
					new(LogEntryLevel.Info, "OtherLogger", "Test message 2"),
					new(LogEntryLevel.Info, "ThirdLogger", "Test message 3")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTermExclude("MyApp.Logger");
				viewer.AddRegexSearchTermExclude("OtherLogger");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEntry>().ToList();

				// Should hide entries matching ANY exclude term
				Assert.Single(visibleItems);
				Assert.Equal("ThirdLogger", visibleItems[0].LoggerName);
			});
		}

		[Fact]
		public void Filter_EmptySearchTerms_ShowsAllEntries()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var testData = new ObservableCollection<LogEntry>
				{
					new(LogEntryLevel.Info, "MyApp.Logger", "Test message 1"),
					new(LogEntryLevel.Info, "OtherLogger", "Test message 2")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act - no search terms added

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEntry>().ToList();

				// Should show all entries when no filters are active
				Assert.Equal(2, visibleItems.Count);
			});
		}

		[Fact]
		public void AddRegexSearchTerm_SpecialRegexCharacters_AreTakenLiterally()
		{
			_context.RunOnSta(() =>
			{
				var viewer = new WpfLogViewer();
				var specialChars = new[] { ".", "*", "+", "?", "^", "$", "[", "]", "(", ")", "{", "}", "|", "\\" };

				foreach (var specialChar in specialChars)
				{
					viewer.ClearAllSearchTerms();
					viewer.AddRegexSearchTerm($"Test{specialChar}Pattern");

					var searchTerm = viewer.ActiveSearchTerms[0];
					Assert.Equal($"Test{specialChar}Pattern", searchTerm.Text);
					Assert.True(searchTerm.Contains($"a Test{specialChar}Pattern b"));
				}
			});
		}

		[Fact]
		public void AddRegexSearchTermExclude_SpecialRegexCharacters_AreTakenLiterally()
		{
			_context.RunOnSta(() =>
			{
				var viewer = new WpfLogViewer();

				viewer.AddRegexSearchTermExclude("MyApp.Logger+Test");

				var searchTerm = viewer.ActiveSearchTerms[0];
				Assert.True(searchTerm.IsExclude);
				Assert.True(searchTerm.Contains("x MyApp.Logger+Test y"));
				Assert.False(searchTerm.Contains("x MyAppXLoggerTest y"));
			});
		}

		[Fact]
		public void Filter_ExcludePatternMatches_EntryIsHidden()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var testData = new ObservableCollection<LogEntry>
				{
					new(LogEntryLevel.Info, "MyApp.Logger", "Test message")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTermExclude("MyApp.Logger");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEntry>().ToList();

				// Entry matching exclude pattern should be hidden
				Assert.Empty(visibleItems);
			});
		}

		[Fact]
		public void Filter_ExcludePatternDoesNotMatch_EntryIsShown()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var testData = new ObservableCollection<LogEntry>
				{
					new(LogEntryLevel.Info, "OtherLogger", "Test message")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTermExclude("MyApp.Logger");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEntry>().ToList();

				// Entry not matching exclude pattern should be shown
				Assert.Single(visibleItems);
				Assert.Equal("OtherLogger", visibleItems[0].LoggerName);
			});
		}

		[Fact]
		public void Filter_MessageField_IsAlsoFiltered()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var testData = new ObservableCollection<LogEntry>
				{
					new(LogEntryLevel.Info, "MyApp.Logger", "Error occurred"),
					new(LogEntryLevel.Info, "OtherLogger", "Info message")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTerm("Error");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEntry>().ToList();

				// Should match in message field
				Assert.Single(visibleItems);
				Assert.Equal("Error occurred", visibleItems[0].Message);
			});
		}

		[Fact]
		public void Filter_ExcludeMessageField_WorksCorrectly()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var testData = new ObservableCollection<LogEntry>
				{
					new(LogEntryLevel.Info, "OtherLogger1", "Error occurred"),
					new(LogEntryLevel.Info, "OtherLogger2", "Info message"),
					new(LogEntryLevel.Info, "OtherLogger3", "error"),
					new(LogEntryLevel.Info, "OtherLogger4", "Error")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTermExclude("Error");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEntry>().ToList();

				// The case does not matter by default, so every entry with "error" in its message is hidden
				var visible = Assert.Single(visibleItems);
				Assert.Equal("OtherLogger2", visible.LoggerName);
			});
		}

		[Fact]
		public void Filter_ExcludeMessageFieldMatchingCase_KeepsTheOtherCase()
		{
			_context.RunOnSta(() =>
			{
				var testData = new ObservableCollection<LogEntry>
				{
					new(LogEntryLevel.Info, "OtherLogger1", "Error occurred"),
					new(LogEntryLevel.Info, "OtherLogger2", "Info message"),
					new(LogEntryLevel.Info, "OtherLogger3", "error"),
					new(LogEntryLevel.Info, "OtherLogger4", "Error")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				viewer.CurrentSearchText = "Error";
				viewer.FilterIsExclude = true;
				viewer.FilterMatchCase = true;
				viewer.AddSearchTerm();

				var visibleItems = ((CollectionView)viewer.LogEvents.View).Cast<LogEntry>().Select(item => item.LoggerName).ToList();
				Assert.Equal(new[] { "OtherLogger2", "OtherLogger3" }, visibleItems);
			});
		}

		public void Dispose()
		{
			// No cleanup needed - each test creates its own viewer instance
		}
	}
}