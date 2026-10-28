using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests that the control follows the language of its texts at once: the texts in the XAML, the names for a screen reader, the headers, the
/// result row and the dialogs, without a new viewer
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerLanguageTests
{
	private readonly WpfStaContextFixture _context;

	public LogViewerLanguageTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private void WithViewer(Action<Wpf.LogViewer> test) => _context.RunOnSta(() =>
	{
		var entries = new ObservableCollection<LogEntry>
		{
			new(LogEntryLevel.Info, "Charlie", "one"),
			new(LogEntryLevel.Error, "Alpha", "two"),
			new(LogEntryLevel.Debug, "Bravo", "three")
		};
		var (window, viewer) = WpfTestHelper.ShowInWindow(entries);
		try
		{
			ApplicationCommands.Find.Execute(null, viewer);
			WpfTestHelper.Settle();
			test(viewer);
		}
		finally
		{
			LogViewerTexts.Language = LogViewerLanguage.English;
			window.Close();
		}
	});

	private static string[] Tips(Wpf.LogViewer viewer) => WpfTestHelper.Descendants<ButtonBase>(viewer).Select(button => button.ToolTip as string).Where(tip => tip != null).ToArray()!;

	[Fact]
	public void TheTextsInTheXaml_AreInTheNewLanguageAtOnce() =>
		WithViewer(viewer =>
		{
			Assert.Contains("Clear: remove all entries from the list", Tips(viewer));
			Assert.Contains("Hide the Warn entries (press again to show them)", Tips(viewer));

			LogViewerTexts.Language = LogViewerLanguage.German;

			Assert.Contains("Leeren: alle Einträge aus der Liste entfernen", Tips(viewer));
			Assert.Contains("Einträge der Stufe Warnung ausblenden (erneut drücken zum Einblenden)", Tips(viewer));
			Assert.DoesNotContain("Clear: remove all entries from the list", Tips(viewer));
		});

	[Fact]
	public void TheNamesForAScreenReader_AndTheHintsOfTheBoxes_FollowTheLanguage() =>
		WithViewer(viewer =>
		{
			var names = () => WpfTestHelper.Descendants<ToggleButton>(viewer).Select(AutomationProperties.GetName).ToArray();
			Assert.Contains("Match case of the search", names());
			Assert.Contains("Match case", names());

			LogViewerTexts.Language = LogViewerLanguage.French;

			Assert.Contains("Respecter la casse dans la recherche", names());
			Assert.Contains("Respecter la casse", names());
			var hints = WpfTestHelper.Descendants<Wpf.Helper.WatermarkTextBox>(viewer).Select(box => box.Hint).ToArray();
			Assert.Equal(new[] { "Filtrer les entrées", "Rechercher dans les entrées" }, hints.Take(2));
		});

	[Fact]
	public void TheHeadersTheResultRowAndTheChipOfTheSort_FollowTheLanguage() =>
		WithViewer(viewer =>
		{
			viewer.CycleSort(LogColumns.Logger);
			Assert.Equal(("Entries: 3", "Sorted by LoggerName ▲"), (viewer.FilterResultText, viewer.SortText));

			LogViewerTexts.Language = LogViewerLanguage.German;

			Assert.Equal(("Einträge: 3", "Sortiert nach Loggername ▲"), (viewer.FilterResultText, viewer.SortText));
			var headers = WpfTestHelper.Descendants<GridViewColumnHeader>(viewer).Where(header => header.Column != null).Select(header => (string)header.Column.Header).ToArray();
			Assert.Contains("Zeitstempel", headers.Select(header => header.Split(" (")[0]));
			Assert.Contains("Loggername ▲", headers);
			Assert.Contains("Nachricht", headers);

			viewer.FindText = "t";
			WpfTestHelper.Settle();
			Assert.Equal("2 Treffer", viewer.FindResultText);
		});

	[Fact]
	public void ThePatternsAndTheMenuOfAHeader_AreMadeInTheNewLanguage() =>
		WithViewer(viewer =>
		{
			LogViewerTexts.Language = LogViewerLanguage.Polish;

			Assert.Equal("Data (2025-12-18)", Wpf.LogViewer.RegexTemplates.First().Name);
			var column = WpfTestHelper.Descendants<GridViewColumnHeader>(viewer).First(header => header.Column != null).Column;
			var items = viewer.BuildColumnMenu(column, null).Items.OfType<MenuItem>().Select(item => (string)item.Header).ToList();
			Assert.Contains("Sortuj rosnąco", items);
			Assert.Contains("Poziom", items);
		});

	[Fact]
	public void TheDialogs_AreMadeInTheLanguageOfTheControl() =>
		WithViewer(viewer =>
		{
			Assert.Equal("Go to time", new Wpf.Helper.GoToTimeDialog(null, null).Title);

			LogViewerTexts.Language = LogViewerLanguage.German;

			Assert.Equal("Zur Zeit springen", new Wpf.Helper.GoToTimeDialog(null, null).Title);
			Assert.Equal("Zeitraum", new Wpf.Helper.TimeRangeDialog(null, null, null, null, null).Title);
		});
}