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
				var theme = Descendants<ComboBox>(window).First();

				Assert.Contains(Descendants<TextBlock>(theme), text => text.Text == "Newschool");
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
