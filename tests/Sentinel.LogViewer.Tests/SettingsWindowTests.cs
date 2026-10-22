using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Sentinel.LogViewer.Controls;
using Sentinel.LogViewer.Tests.ViewModels;
using Sentinel.LogViewer.ViewModels.Settings;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests that the settings window loads and that its controls are connected to the settings
/// </summary>
[Collection(WpfCollection.Name)]
public class SettingsWindowTests
{
	private readonly WpfStaContextFixture _context;

	public SettingsWindowTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	[Fact]
	public void Window_ShowsACardPerSettingOfTheSelectedCategory()
	{
		_context.RunOnSta(() =>
		{
			using var environment = new SettingsTestEnvironment();
			var viewModel = environment.CreateViewModel();
			var window = ShowOffScreen(new SettingsWindow(viewModel));
			try
			{
				Assert.Equal(viewModel.Category("General").Settings.Count, Descendants<SettingCard>(window).Count());

				viewModel.SelectedCategory = viewModel.Category("Receiving");
				WpfTestHelper.Settle();
				Assert.Equal(viewModel.Category("Receiving").Settings.Count, Descendants<SettingCard>(window).Count());

				viewModel.SearchText = "github";
				WpfTestHelper.Settle();
				Assert.Single(Descendants<SettingCard>(window));
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void TheLoggerTreeSettings_AreRowsInOneCard_AndTheirControlsAreConnected()
	{
		_context.RunOnSta(() =>
		{
			using var environment = new SettingsTestEnvironment();
			var viewModel = environment.CreateViewModel();
			viewModel.SelectedCategory = viewModel.Category("Appearance");
			var window = ShowOffScreen(new SettingsWindow(viewModel));
			try
			{
				var rows = Descendants<SettingCard>(window).Where(card => card.Setting?.Group?.Title == "Logger tree").ToList();
				Assert.Equal(new[] { "Side", "Open", "Width in pixels" }, rows.Select(row => row.Setting!.Title));
				Assert.Contains(Descendants<TextBlock>(window), text => text.Text == "Default view for new tabs");
				Assert.Equal(rows[0].Setting!.Description, Descendants<Border>(rows[0]).First(border => border.ToolTip != null).ToolTip);

				var toggle = Descendants<ToggleButton>(rows[1]).Single();
				toggle.IsChecked = true;
				Assert.True(environment.Saved.LoggerTreeOpen);

				var box = Descendants<TextBox>(rows[2]).Single();
				box.Text = "333";
				box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
				Assert.Equal(333, environment.Saved.LoggerTreeWidth);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void DropDown_ShowsTheNameOfTheSelectedOption()
	{
		_context.RunOnSta(() =>
		{
			using var environment = new SettingsTestEnvironment();
			var viewModel = environment.CreateViewModel();
			viewModel.SelectedCategory = viewModel.Category("Appearance");
			var window = ShowOffScreen(new SettingsWindow(viewModel));
			try
			{
				var mode = Descendants<ComboBox>(window).First();

				Assert.Contains(Descendants<TextBlock>(mode), text => text.Text == "Like Windows");
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void TheListAppearance_IsOneCard_WhosePreviewFollowsEveryControl()
	{
		_context.RunOnSta(() =>
		{
			using var environment = new SettingsTestEnvironment();
			var viewModel = environment.CreateViewModel();
			viewModel.SelectedCategory = viewModel.Category("Appearance");
			var window = ShowOffScreen(new SettingsWindow(viewModel));
			try
			{
				var look = viewModel.Setting<ListLookSettingViewModel>("List appearance");
				var card = Descendants<SettingCard>(window).Single(candidate => candidate.Setting == look);
				var boxes = Descendants<ComboBox>(card).ToList();
				Assert.Equal(7, boxes.Count);
				var (mode, theme, format, level, height, font, size) = (boxes[0], boxes[1], boxes[2], boxes[3], boxes[4], boxes[5], boxes[6]);
				Assert.True(size.IsEditable);
				Assert.DoesNotContain(Descendants<SettingCard>(window), candidate => candidate.Setting?.IsEmbedded == true);

				TextBlock Message() => Descendants<TextBlock>(card).Single(text => text.Text == "Payment failed: timeout");
				TextBlock LevelText() => Descendants<TextBlock>(card).Single(text => text.Text == "Error");
				Image Symbol() => Descendants<Image>(card).Last();
				var normalHeight = Symbol().Height;

				// The controls change the settings, and the preview follows each of them.
				format.SelectedValue = "german";
				WpfTestHelper.Settle();
				Assert.Contains(Descendants<TextBlock>(card), text => text.Text == "18.12.2025 15:11:48.385");

				theme.SelectedValue = "oldschool";
				mode.SelectedValue = "dark";
				WpfTestHelper.Settle();
				Assert.Equal(("dark", "oldschool"), (environment.Saved.ThemeMode, environment.Saved.Theme));

				level.SelectedValue = "SymbolAndText";
				WpfTestHelper.Settle();
				Assert.Equal(Visibility.Visible, LevelText().Visibility);
				Assert.Equal(Visibility.Visible, Symbol().Visibility);
				level.SelectedValue = "Text";
				WpfTestHelper.Settle();
				Assert.Equal(Visibility.Collapsed, Symbol().Visibility);

				height.SelectedValue = "Compact";
				WpfTestHelper.Settle();
				Assert.True(Symbol().Height < normalHeight);

				font.SelectedValue = "Consolas";
				WpfTestHelper.Settle();
				Assert.Equal("Consolas", Message().FontFamily.Source);
				Assert.Equal("Consolas", Descendants<TextBlock>(font).Single(text => text.Text == "Consolas").FontFamily.Source);

				size.SelectedItem = "16";
				WpfTestHelper.Settle();
				Assert.Equal(16, Message().FontSize);
				Assert.Equal(16, environment.Saved.ListFontSize);

				size.Text = "18";
				size.GetBindingExpression(ComboBox.TextProperty)!.UpdateSource();
				WpfTestHelper.Settle();
				Assert.Equal(18, Message().FontSize);
				Assert.Equal(18, environment.Saved.ListFontSize);

				size.Text = "5";
				size.GetBindingExpression(ComboBox.TextProperty)!.UpdateSource();
				WpfTestHelper.Settle();
				Assert.Equal(18, Message().FontSize);
				Assert.NotNull(look.Error);
				Assert.Contains(Descendants<TextBlock>(card), text => text.Text == look.Error);

				var saved = environment.Saved;
				Assert.Equal(("Text", "Compact", "Consolas"), (saved.LevelDisplay, saved.RowHeight, saved.ListFontFamily));

				// The button takes back the last change, which is the size, and the preview follows.
				var undo = Descendants<Button>(card).Single(button => button.Command == look.UndoCommand);
				Assert.True(undo.IsEnabled);
				look.SizeText = "20";
				WpfTestHelper.Settle();
				Assert.Equal(20, Message().FontSize);
				undo.Command.Execute(null);
				WpfTestHelper.Settle();
				Assert.Equal(18, Message().FontSize);
				Assert.Equal(18, environment.Saved.ListFontSize);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void TheTimeBetweenNotifications_IsADropDownThatTakesATypedNumber()
	{
		_context.RunOnSta(() =>
		{
			using var environment = new SettingsTestEnvironment();
			var viewModel = environment.CreateViewModel();
			var window = ShowOffScreen(new SettingsWindow(viewModel));
			try
			{
				var interval = viewModel.Setting<IntChoiceSettingViewModel>("Time between notifications in the tray (seconds)");
				var box = Descendants<ComboBox>(window).Single(candidate => ReferenceEquals(candidate.ItemsSource, interval.Suggestions));
				Assert.True(box.IsEditable);

				box.SelectedItem = "60";
				WpfTestHelper.Settle();
				Assert.Equal(60, environment.Saved.TrayNotifyIntervalSeconds);

				box.Text = "90";
				box.GetBindingExpression(ComboBox.TextProperty)!.UpdateSource();
				WpfTestHelper.Settle();
				Assert.Equal(90, environment.Saved.TrayNotifyIntervalSeconds);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void Switch_WhenToggled_SavesTheSetting()
	{
		_context.RunOnSta(() =>
		{
			using var environment = new SettingsTestEnvironment();
			var viewModel = environment.CreateViewModel();
			var window = ShowOffScreen(new SettingsWindow(viewModel));
			try
			{
				viewModel.SelectedCategory = viewModel.Category("Receiving");
				WpfTestHelper.Settle();
				var autoStart = Descendants<ToggleButton>(window).Single(toggle => toggle.DataContext == viewModel.Setting<BoolSettingViewModel>("Start listening on launch"));

				autoStart.IsChecked = true;

				Assert.True(environment.Saved.AutoStartListening);
				Assert.True(((SettingsWindow)window).HasChanges);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void NumberBox_WhenCommitted_SavesTheSettingOrShowsTheError()
	{
		_context.RunOnSta(() =>
		{
			using var environment = new SettingsTestEnvironment();
			var viewModel = environment.CreateViewModel();
			var window = ShowOffScreen(new SettingsWindow(viewModel));
			try
			{
				viewModel.SelectedCategory = viewModel.Category("Receiving");
				WpfTestHelper.Settle();
				var setting = viewModel.Setting<IntSettingViewModel>("Maximum entries per tab");
				var box = Descendants<TextBox>(window).Single(textBox => textBox.DataContext == setting);

				box.Text = "12";
				box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
				Assert.True(setting.HasError);
				Assert.Equal(10000, environment.Saved.MaxLogEntriesPerTab);

				box.Text = "7500";
				box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
				Assert.False(setting.HasError);
				Assert.Equal(7500, environment.Saved.MaxLogEntriesPerTab);
			}
			finally
			{
				window.Close();
			}
		});
	}

	private static Window ShowOffScreen(Window window)
	{
		window.Left = -10000;
		window.Top = -10000;
		window.ShowActivated = false;
		window.ShowInTaskbar = false;
		window.Show();
		WpfTestHelper.Settle();
		return window;
	}

	private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
	{
		for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
		{
			var child = VisualTreeHelper.GetChild(root, i);
			if (child is T match)
				yield return match;

			foreach (var descendant in Descendants<T>(child))
				yield return descendant;
		}
	}
}
