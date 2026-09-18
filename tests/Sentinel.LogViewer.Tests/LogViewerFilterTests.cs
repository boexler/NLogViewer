using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows.Data;
using NLog;
using Sentinel.LogViewer.Wpf;
using Xunit;
using WpfLogViewer = Sentinel.LogViewer.Wpf.LogViewer;

namespace Sentinel.LogViewer.Tests
{
	/// <summary>
	/// Unit tests for LogViewer filter commands (AddRegexSearchTerm and AddRegexSearchTermExclude)
	/// </summary>
	public class LogViewerFilterTests : IClassFixture<WpfStaContextFixture>, IDisposable
	{
		private readonly WpfStaContextFixture _context;

		public LogViewerFilterTests(WpfStaContextFixture fixture)
		{
			_context = fixture ?? throw new ArgumentNullException(nameof(fixture));
		}

		[Fact]
		public void AddRegexSearchTerm_EscapesSpecialCharacters_CreatesLiteralPattern()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var viewer = new WpfLogViewer();
				string loggerName = "MyApp.Logger";
				string expectedEscaped = Regex.Escape(loggerName); // Should be "MyApp\\.Logger"

				// Act
				viewer.AddRegexSearchTerm(loggerName);

				// Assert
				Assert.Single(viewer.ActiveSearchTerms);
				var searchTerm = viewer.ActiveSearchTerms[0];
				Assert.IsType<RegexSearchTerm>(searchTerm);
				Assert.Equal(expectedEscaped, searchTerm.Text);
				// Verify it's not an exclude pattern
				Assert.DoesNotMatch("^\\^\\(\\?!\\.\\*", searchTerm.Text);
			});
		}

		[Fact]
		public void AddRegexSearchTerm_LoggerNameWithDot_MatchesExactLoggerName()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var testData = new ObservableCollection<LogEventInfo>
				{
					new(LogLevel.Info, "MyApp.Logger", "Test message 1"),
					new(LogLevel.Info, "MyAppLogger", "Test message 2"),
					new(LogLevel.Info, "MyApp.Other", "Test message 3")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTerm("MyApp.Logger");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEventInfo>().ToList();

				// Should only match "MyApp.Logger", not "MyAppLogger" or "MyApp.Other"
				Assert.Single(visibleItems);
				Assert.Equal("MyApp.Logger", visibleItems[0].LoggerName);
			});
		}

		[Fact]
		public void AddRegexSearchTermExclude_CreatesNegativeLookaheadPattern()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var viewer = new WpfLogViewer();
				string loggerName = "MyApp.Logger";
				string expectedPattern = $"^(?!.*{Regex.Escape(loggerName)}$)";

				// Act
				viewer.AddRegexSearchTermExclude(loggerName);

				// Assert
				Assert.Single(viewer.ActiveSearchTerms);
				var searchTerm = viewer.ActiveSearchTerms[0];
				Assert.IsType<RegexSearchTerm>(searchTerm);
				Assert.StartsWith("^(?!.*", searchTerm.Text);
				Assert.Equal(expectedPattern, searchTerm.Text);
			});
		}

		[Fact]
		public void AddRegexSearchTermExclude_ExcludesMatchingEntries()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var testData = new ObservableCollection<LogEventInfo>
				{
					new(LogLevel.Info, "MyApp.Logger", "Test message 1"),
					new(LogLevel.Info, "OtherLogger", "Test message 2"),
					new(LogLevel.Info, "MyApp.Other", "Test message 3")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTermExclude("MyApp.Logger");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEventInfo>().ToList();

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
				var testData = new ObservableCollection<LogEventInfo>
				{
					new(LogLevel.Info, "MyApp.Logger", "Test message 1"),
					new(LogLevel.Info, "OtherLogger", "Test message 2")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTermExclude("MyApp.Logger");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEventInfo>().ToList();

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
				var testData = new ObservableCollection<LogEventInfo>
				{
					new(LogLevel.Info, "MyApp.Logger", "Error occurred"),
					new(LogLevel.Info, "MyApp.Logger", "Info message"),
					new(LogLevel.Info, "OtherLogger", "Error occurred"),
					new(LogLevel.Info, "OtherLogger", "Info message")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTerm("Error"); // Include: must contain "Error"
				viewer.AddRegexSearchTermExclude("MyApp.Logger"); // Exclude: must not contain "MyApp.Logger"

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEventInfo>().ToList();

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
				var testData = new ObservableCollection<LogEventInfo>
				{
					new(LogLevel.Info, "MyApp.Logger", "Error occurred"),
					new(LogLevel.Info, "MyApp.Logger", "Warning message"),
					new(LogLevel.Info, "OtherLogger", "Error occurred"),
					new(LogLevel.Info, "OtherLogger", "Warning message")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTerm("Error");
				viewer.AddRegexSearchTerm("MyApp");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEventInfo>().ToList();

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
				var testData = new ObservableCollection<LogEventInfo>
				{
					new(LogLevel.Info, "MyApp.Logger", "Test message 1"),
					new(LogLevel.Info, "OtherLogger", "Test message 2"),
					new(LogLevel.Info, "ThirdLogger", "Test message 3")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTermExclude("MyApp.Logger");
				viewer.AddRegexSearchTermExclude("OtherLogger");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEventInfo>().ToList();

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
				var testData = new ObservableCollection<LogEventInfo>
				{
					new(LogLevel.Info, "MyApp.Logger", "Test message 1"),
					new(LogLevel.Info, "OtherLogger", "Test message 2")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act - no search terms added

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEventInfo>().ToList();

				// Should show all entries when no filters are active
				Assert.Equal(2, visibleItems.Count);
			});
		}

		[Fact]
		public void AddRegexSearchTerm_SpecialRegexCharacters_AreEscaped()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var viewer = new WpfLogViewer();
				var specialChars = new[] { ".", "*", "+", "?", "^", "$", "[", "]", "(", ")", "{", "}", "|", "\\" };

				foreach (var specialChar in specialChars)
				{
					// Act
					viewer.ClearAllSearchTerms();
					viewer.AddRegexSearchTerm($"Test{specialChar}Pattern");

					// Assert
					var searchTerm = viewer.ActiveSearchTerms[0];
					Assert.IsType<RegexSearchTerm>(searchTerm);
					// The pattern should be escaped, so special characters should be backslash-escaped
					var escapedPattern = Regex.Escape($"Test{specialChar}Pattern");
					Assert.Equal(escapedPattern, searchTerm.Text);
				}
			});
		}

		[Fact]
		public void AddRegexSearchTermExclude_SpecialRegexCharacters_AreEscapedInNegativeLookahead()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var viewer = new WpfLogViewer();
				string loggerName = "MyApp.Logger+Test";
				string expectedEscaped = Regex.Escape(loggerName);
				string expectedPattern = $"^(?!.*{expectedEscaped}$)";

				// Act
				viewer.AddRegexSearchTermExclude(loggerName);

				// Assert
				var searchTerm = viewer.ActiveSearchTerms[0];
				Assert.IsType<RegexSearchTerm>(searchTerm);
				Assert.Equal(expectedPattern, searchTerm.Text);
				// Verify it starts with negative lookahead
				Assert.StartsWith("^(?!.*", searchTerm.Text);
			});
		}

		[Fact]
		public void Filter_ExcludePatternMatches_EntryIsHidden()
		{
			_context.RunOnSta(() =>
			{
				// Arrange
				var testData = new ObservableCollection<LogEventInfo>
				{
					new(LogLevel.Info, "MyApp.Logger", "Test message")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTermExclude("MyApp.Logger");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEventInfo>().ToList();

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
				var testData = new ObservableCollection<LogEventInfo>
				{
					new(LogLevel.Info, "OtherLogger", "Test message")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTermExclude("MyApp.Logger");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEventInfo>().ToList();

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
				var testData = new ObservableCollection<LogEventInfo>
				{
					new(LogLevel.Info, "MyApp.Logger", "Error occurred"),
					new(LogLevel.Info, "OtherLogger", "Info message")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTerm("Error");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEventInfo>().ToList();

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
				var testData = new ObservableCollection<LogEventInfo>
				{
					new(LogLevel.Info, "OtherLogger1", "Error occurred"),
					new(LogLevel.Info, "OtherLogger2", "Info message"),
					new(LogLevel.Info, "OtherLogger3", "error"),
					new(LogLevel.Info, "OtherLogger4", "Error")
				};
				var viewer = WpfTestHelper.CreateViewerWithTestData(testData);

				// Act
				viewer.AddRegexSearchTermExclude("Error");

				// Assert
				var collectionView = (CollectionView)viewer.LogEvents.View;
				var visibleItems = collectionView.Cast<LogEventInfo>().ToList();

				// Should exclude the last item
				Assert.Equal(3, visibleItems.Count);
				Assert.Contains(visibleItems, item => item.LoggerName == "OtherLogger1");
				Assert.Contains(visibleItems, item => item.LoggerName == "OtherLogger2");
				Assert.Contains(visibleItems, item => item.LoggerName == "OtherLogger3");
				Assert.DoesNotContain(visibleItems, item => item.LoggerName == "OtherLogger4");
			});
		}

		public void Dispose()
		{
			// No cleanup needed - each test creates its own viewer instance
		}
	}
}