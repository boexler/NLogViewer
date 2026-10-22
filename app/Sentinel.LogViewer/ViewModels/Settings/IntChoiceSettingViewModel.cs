using System;
using System.Collections.Generic;
using System.Linq;

namespace Sentinel.LogViewer.ViewModels.Settings;

/// <summary>
/// A whole number within limits that is chosen from a list of suggestions in a drop-down or typed into it
/// </summary>
public sealed class IntChoiceSettingViewModel(string categoryId, string icon, string title, string description,
	Func<int> get, Action<int> set, Func<int> getDefault, Func<int, bool> isValid, string invalidMessage, IReadOnlyList<string> suggestions)
	: IntSettingViewModel(categoryId, icon, title, description, get, set, getDefault, isValid, invalidMessage)
{
	public IReadOnlyList<string> Suggestions { get; } = suggestions;

	/// <summary>The suggestion that is the value now, null when the value is another number</summary>
	public string? SelectedSuggestion
	{
		get => Suggestions.Contains(Text) ? Text : null;
		set
		{
			// The drop-down picks the suggestion again whenever it shows the value, which must not clear the error of an input that was refused.
			if (value != null && value != Text)
				Text = value;
		}
	}

	public override void Reload()
	{
		base.Reload();
		OnPropertyChanged(nameof(SelectedSuggestion));
	}
}
