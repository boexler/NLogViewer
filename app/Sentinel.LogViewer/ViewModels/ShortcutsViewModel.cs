using System.Collections.Generic;
using System.Linq;
using Sentinel.LogViewer.Services;

namespace Sentinel.LogViewer.ViewModels;

/// <summary>One command of the window: each way to give it (<c>Ctrl + W</c>) and what it does</summary>
public sealed record ShortcutItem(IReadOnlyList<string> Keys, string Text);

public sealed record ShortcutGroupItem(string Title, IReadOnlyList<ShortcutItem> Items);

/// <summary>
/// The commands of <see cref="ShortcutCatalog"/> in the language of the user
/// </summary>
public sealed class ShortcutsViewModel(LocalizationService localization)
{
	public string Title { get; } = localization.GetString("Shortcuts_Title", "Keyboard and mouse commands");

	public IReadOnlyList<ShortcutGroupItem> Groups { get; } = ShortcutCatalog.Groups
		.Select(group => new ShortcutGroupItem(
			localization.GetString(group.TitleKey, group.TitleDefault),
			group.Shortcuts.Select(shortcut => new ShortcutItem(
				shortcut.Keys.Split(" / ").Select(combination => string.Join(" + ", combination.Split('+').Select(part => Name(localization, part)))).ToList(),
				localization.GetString(shortcut.TextKey, shortcut.TextDefault))).ToList()))
		.ToList();

	private static string Name(LocalizationService localization, string part) =>
		ShortcutCatalog.LocalizedKeys.Contains(part) ? localization.GetString("Key_" + part, part) : part;
}
