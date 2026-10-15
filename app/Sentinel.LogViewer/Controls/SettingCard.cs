using System.Windows;
using System.Windows.Controls;
using Sentinel.LogViewer.ViewModels.Settings;

namespace Sentinel.LogViewer.Controls;

/// <summary>
/// A card of the settings window: icon, title and description of a setting on the left, its control (the content)
/// on the right, and optional details below, e.g. a list
/// </summary>
public class SettingCard : ContentControl
{
	public static readonly DependencyProperty SettingProperty =
		DependencyProperty.Register(nameof(Setting), typeof(SettingViewModel), typeof(SettingCard), new PropertyMetadata(null));

	public static readonly DependencyProperty DetailsProperty =
		DependencyProperty.Register(nameof(Details), typeof(object), typeof(SettingCard), new PropertyMetadata(null));

	/// <summary>
	/// The setting that provides icon, title and description
	/// </summary>
	public SettingViewModel? Setting
	{
		get => (SettingViewModel?)GetValue(SettingProperty);
		set => SetValue(SettingProperty, value);
	}

	/// <summary>
	/// Content below the title row
	/// </summary>
	public object? Details
	{
		get => GetValue(DetailsProperty);
		set => SetValue(DetailsProperty, value);
	}
}
