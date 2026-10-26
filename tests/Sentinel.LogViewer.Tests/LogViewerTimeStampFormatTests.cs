using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using Sentinel.LogViewer.Wpf.Models;
using Sentinel.LogViewer.Wpf.Resolver;
using Xunit;
using static Sentinel.LogViewer.Tests.WpfTestHelper;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for the format of the time stamp column of the log viewer: a default, a choice per viewer and the context menus
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerTimeStampFormatTests
{
	private readonly WpfStaContextFixture _context;

	public LogViewerTimeStampFormatTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static ObservableCollection<LogEntry> Entries() => new()
	{
		new LogEntry(LogEntryLevel.Info, "Logger", "message") { TimeStamp = new DateTime(2025, 12, 18, 15, 11, 48, 385) },
		new LogEntry(LogEntryLevel.Warn, "Logger", "other") { TimeStamp = new DateTime(2025, 12, 18, 15, 11, 49, 100) }
	};

	private static string Resolved(Wpf.LogViewer viewer) => viewer.TimeStampResolver.Resolve(Entries()[0]);

	[Fact]
	public void TheViewerFollowsTheDefault_UntilItHasAFormatOfItsOwn()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				viewer.DefaultTimeStampFormat = TimeStampFormat.German;
				Assert.Equal(TimeStampFormat.German, viewer.EffectiveTimeStampFormat);
				Assert.Equal("18.12.2025 15:11:48.385", Resolved(viewer));

				viewer.CustomTimeStampFormat = TimeStampFormat.English;
				Assert.Equal("12/18/2025 03:11:48.385 PM", Resolved(viewer));

				viewer.DefaultTimeStampFormat = TimeStampFormat.Iso;
				Assert.Equal(TimeStampFormat.English, viewer.EffectiveTimeStampFormat);

				viewer.CustomTimeStampFormat = null;
				Assert.Equal("2025-12-18 15:11:48.385", Resolved(viewer));
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void SetAndResetCommands_ChangeTheFormatOfTheViewer()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				viewer.DefaultTimeStampFormat = TimeStampFormat.Iso;
				Assert.False(viewer.ResetTimeStampFormatCommand.CanExecute(null));

				viewer.SetTimeStampFormatCommand.Execute(TimeStampFormat.German);
				Assert.Equal(TimeStampFormat.German, viewer.CustomTimeStampFormat);
				Assert.True(viewer.ResetTimeStampFormatCommand.CanExecute(null));

				viewer.ResetTimeStampFormatCommand.Execute(null);
				Assert.Null(viewer.CustomTimeStampFormat);
				Assert.Equal(TimeStampFormat.Iso, viewer.EffectiveTimeStampFormat);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void AResolverOfYourOwn_IsKeptAndTheFormatCannotBeChanged()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				var own = new OwnResolver();
				viewer.TimeStampResolver = own;

				Assert.False(viewer.CanChangeTimeStampFormat);
				Assert.False(viewer.SetTimeStampFormatCommand.CanExecute(TimeStampFormat.Iso));

				viewer.DefaultTimeStampFormat = TimeStampFormat.German;
				viewer.CustomTimeStampFormat = TimeStampFormat.Iso;
				Assert.Same(own, viewer.TimeStampResolver);

				viewer.TimeStampResolver = new TimeStampResolver();
				Assert.True(viewer.CanChangeTimeStampFormat);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void TheCellsShowTheFormat_AndTheColumnIsSizedToIt()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				viewer.CustomTimeStampFormat = TimeStampFormat.Iso;
				WpfTestHelper.Settle();
				var listView = (ListView)viewer.Template.FindName("PART_ListView", viewer);
				var timeStampColumn = ((GridView)listView.View).Columns.Single(column => column.Header as string == "TimeStamp");
				var isoWidth = timeStampColumn.ActualWidth;
				Assert.Contains("2025-12-18 15:11:48.385", CellTexts(listView));

				viewer.CustomTimeStampFormat = TimeStampFormat.English;
				WpfTestHelper.Settle();

				Assert.Contains("12/18/2025 03:11:48.385 PM", CellTexts(listView));
				Assert.True(timeStampColumn.ActualWidth > isoWidth, $"{timeStampColumn.ActualWidth} should be wider than {isoWidth}");
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void HeaderAndCell_OfTheTimeStampColumn_OfferTheFormatsAndTheDefaultView()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				viewer.UpdateLayout();
				var listView = (ListView)viewer.Template.FindName("PART_ListView", viewer);
				var header = Descendants<GridViewColumnHeader>(listView).Single(candidate => candidate.Content as string == "TimeStamp");
				var cell = Descendants<TextBlock>((DependencyObject)listView.ItemContainerGenerator.ContainerFromIndex(0))
					.First(text => text.ContextMenu?.Items.OfType<MenuItem>().Any(item => item.Header as string == "Default view") == true);

				var formats = new[] { "System", "ISO  2025-12-18 15:11:48.385", "German  18.12.2025 15:11:48.385", "English  12/18/2025 03:11:48.385 PM", "Default view" };
				var headerFormats = viewer.BuildColumnMenu(header.Column, header).Items.OfType<MenuItem>().Single(item => item.Header as string == "Timestamp format");
				Assert.Equal(formats, headerFormats.Items.OfType<MenuItem>().Select(item => (string)item.Header));
				Assert.Equal(formats, cell.ContextMenu.Items.OfType<MenuItem>().Skip(1).Select(item => (string)item.Header));
				Assert.Same(viewer, cell.Tag);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void TheMenu_ChecksTheCurrentFormat_AndSwitchesWithItsEntries()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				viewer.DefaultTimeStampFormat = TimeStampFormat.Iso;
				viewer.UpdateLayout();
				var listView = (ListView)viewer.Template.FindName("PART_ListView", viewer);
				var cell = Descendants<TextBlock>((DependencyObject)listView.ItemContainerGenerator.ContainerFromIndex(0))
					.First(text => text.ContextMenu?.Items.OfType<MenuItem>().Any(item => item.Header as string == "Default view") == true);
				var menu = cell.ContextMenu;
				menu.PlacementTarget = cell;
				menu.IsOpen = true;
				WpfTestHelper.Settle();
				try
				{
					var items = menu.Items.OfType<MenuItem>().ToList();
					Assert.Equal(new[] { false, true, false, false }, items.Skip(1).Take(4).Select(item => item.IsChecked));
					Assert.False(items.Last().IsEnabled);

					items[3].Command.Execute(items[3].CommandParameter);
					WpfTestHelper.Settle();

					Assert.Equal(TimeStampFormat.German, viewer.CustomTimeStampFormat);
					Assert.Equal(new[] { false, false, true, false }, items.Skip(1).Take(4).Select(item => item.IsChecked));
					Assert.True(items.Last().Command.CanExecute(null));
				}
				finally
				{
					menu.IsOpen = false;
				}
			}
			finally
			{
				window.Close();
			}
		});
	}

	private static IEnumerable<string> CellTexts(ListView listView)
	{
		for (var i = 0; i < listView.Items.Count; i++)
		{
			if (listView.ItemContainerGenerator.ContainerFromIndex(i) is not DependencyObject row)
				continue;

			foreach (var text in Descendants<TextBlock>(row))
				yield return text.Text;
		}
	}

	private sealed class OwnResolver : ILogEntryResolver
	{
		public string Resolve(LogEntry logEventInfo) => logEventInfo.TimeStamp.ToString("HH:mm", CultureInfo.InvariantCulture);
	}
}
