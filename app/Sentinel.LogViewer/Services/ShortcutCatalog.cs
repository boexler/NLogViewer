namespace Sentinel.LogViewer.Services;

/// <summary>One keyboard or mouse command: the keys and the key of its text in the resources</summary>
/// <param name="Keys">Alternatives separated by " / "; each is a combination whose parts are joined by "+", e.g. <c>Ctrl+Shift+Tab</c></param>
public sealed record Shortcut(string Keys, string TextKey, string TextDefault);

public sealed record ShortcutGroup(string TitleKey, string TitleDefault, IReadOnlyList<Shortcut> Shortcuts);

/// <summary>
/// Every keyboard and mouse command of the application, for the window Help &gt; Keyboard shortcuts. A command that is added to the
/// application belongs here as well.
/// </summary>
public static class ShortcutCatalog
{
	/// <summary>The parts of a combination that are written in the language of the user (the others, e.g. letters and F keys, are shown as they are)</summary>
	public static IReadOnlyList<string> LocalizedKeys { get; } = new[]
	{
		"Ctrl", "Shift", "Alt", "Esc", "Enter", "PageUp", "PageDown", "Tab", "Plus", "Minus", "Wheel", "RightClick", "MiddleClick", "Drag"
	};

	public static IReadOnlyList<ShortcutGroup> Groups { get; } = new ShortcutGroup[]
	{
		new("Shortcuts_Group_Main", "Main window", new Shortcut[]
		{
			new("RightClick", "Shortcut_SourceMenu", "On the receive button of the status bar: switch single sources on or off"),
			new("Ctrl+O", "Shortcut_OpenFile", "Open a log file"),
			new("Ctrl+,", "Shortcut_Settings", "Open the settings"),
			new("F1", "Shortcut_Help", "Show this list of keyboard and mouse commands"),
			new("Alt+F4", "Shortcut_Exit", "Exit the application"),
			new("Drag", "Shortcut_DropFiles", "Drag log files onto the window to open them"),
		}),
		new("Shortcuts_Group_Tabs", "Tabs", new Shortcut[]
		{
			new("Ctrl+W / Ctrl+F4", "Shortcut_CloseTab", "Close the current tab"),
			new("MiddleClick", "Shortcut_MiddleCloseTab", "Close a tab"),
			new("Ctrl+PageDown / Ctrl+Tab", "Shortcut_NextTab", "Go to the next tab"),
			new("Ctrl+PageUp / Ctrl+Shift+Tab", "Shortcut_PreviousTab", "Go to the previous tab"),
			new("Alt+1…8", "Shortcut_GoToTab", "Go to the tab with that number"),
			new("Alt+9", "Shortcut_LastTab", "Go to the last tab"),
			new("Drag", "Shortcut_ReorderTabs", "Drag a tab header to change the order of the tabs"),
			new("RightClick", "Shortcut_TabMenu", "Open the menu of a tab: close, save the filtered list, ignore the sender and more"),
		}),
		new("Shortcuts_Group_List", "Log list", new Shortcut[]
		{
			new("Ctrl+Wheel / Ctrl+Plus / Ctrl+Minus", "Shortcut_Zoom", "Zoom the log list for this session; the status bar offers to save the size"),
			new("Ctrl+0", "Shortcut_ZoomReset", "Go back to the saved font size"),
			new("RightClick", "Shortcut_ColumnMenu", "Choose the columns of the list; the timestamp column also has its format here"),
			new("Drag", "Shortcut_ReorderColumns", "Drag a column header to change the order of the columns"),
			new("Drag", "Shortcut_ResizeColumn", "Drag the edge of a column header to change the width of the column"),
			new("RightClick", "Shortcut_EntryMenu", "Open the menu of an entry: show only or hide a logger, copy the text, add it to the filter"),
			new("RightClick", "Shortcut_LoggerTreeMenu", "On the logger tree: show only one logger or all, expand or collapse all"),
		}),
		new("Shortcuts_Group_Filter", "Filter and search", new Shortcut[]
		{
			new("Enter", "Shortcut_AddFilter", "Add the text of the filter box as a filter term"),
			new("Enter / F3", "Shortcut_NextHit", "Go to the next hit of the search"),
			new("Shift+Enter / Shift+F3", "Shortcut_PreviousHit", "Go to the previous hit of the search"),
		}),
		new("Shortcuts_Group_Settings", "Settings and dialogs", new Shortcut[]
		{
			new("Ctrl+F", "Shortcut_SettingsSearch", "Go to the search box of the settings or of the changelog"),
			new("Esc", "Shortcut_Escape", "Clear the search of the settings, then close the window; closes dialogs"),
			new("Enter", "Shortcut_ConfirmText", "Save the value of a text box in the settings right away"),
		}),
	};

}
