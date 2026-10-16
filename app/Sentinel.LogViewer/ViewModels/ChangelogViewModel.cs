using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Updates;
using Sentinel.LogViewer.Wpf;

namespace Sentinel.LogViewer.ViewModels;

/// <summary>
/// One line of the list of versions: a version, or the entry that gathers everything since the version that was installed before
/// </summary>
public sealed class ChangelogListItem
{
	public ChangelogListItem(string title, string subtitle, ChangelogEntry? entry = null, IReadOnlyList<ChangelogEntry>? entries = null)
	{
		Title = title;
		Subtitle = subtitle;
		Entry = entry;
		Entries = entries ?? (entry == null ? Array.Empty<ChangelogEntry>() : new[] { entry });
	}

	public string Title { get; }

	public string Subtitle { get; }

	/// <summary>The version, or null for the entry that gathers several versions</summary>
	public ChangelogEntry? Entry { get; }

	/// <summary>The versions whose notes this line shows</summary>
	public IReadOnlyList<ChangelogEntry> Entries { get; }

	/// <summary>Whether this is the entry for everything since the version that was installed before</summary>
	public bool IsSince => Entry == null;

	public bool IsInstalled { get; init; }

	/// <summary>Whether the version came after the one that was installed before</summary>
	public bool IsNew { get; init; }

	public bool IsPrerelease => Entry?.IsPrerelease == true;

	public string InstalledText { get; init; } = string.Empty;

	public string NewText { get; init; } = string.Empty;

	public string PrereleaseText { get; init; } = string.Empty;
}

/// <summary>
/// ViewModel of the changelog window: all versions on the left, the notes of the selected one on the right
/// </summary>
public class ChangelogViewModel : INotifyPropertyChanged
{
	private readonly ChangelogService _changelogService;
	private readonly ConfigurationService _configurationService;
	private readonly LocalizationService _localizationService;
	private readonly ReleaseVersion _current;
	private readonly ReleaseVersion? _previous;
	private IReadOnlyList<ChangelogEntry> _entries = Array.Empty<ChangelogEntry>();
	private List<ChangelogListItem> _items = new();
	private ChangelogListItem? _selectedItem;
	private string _searchText = string.Empty;
	private string _statusText = string.Empty;
	private bool _isLoading;

	/// <param name="current">The running version, which is marked in the list</param>
	/// <param name="previous">The version that was installed before, or null; the versions after it are marked as new</param>
	public ChangelogViewModel(ChangelogService changelogService, ConfigurationService configurationService,
		LocalizationService localizationService, ReleaseVersion current, ReleaseVersion? previous = null)
	{
		_changelogService = changelogService ?? throw new ArgumentNullException(nameof(changelogService));
		_configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
		_localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
		_current = current ?? throw new ArgumentNullException(nameof(current));
		_previous = previous != null && previous.CompareTo(current) < 0 ? previous : null;

		OpenOnGitHubCommand = new RelayCommand(OpenOnGitHub, () => OpenUrl != null);
	}

	/// <summary>
	/// The lines of the list that match the search
	/// </summary>
	public ObservableCollection<ChangelogListItem> Items { get; } = new();

	public ChangelogListItem? SelectedItem
	{
		get => _selectedItem;
		set
		{
			if (ReferenceEquals(_selectedItem, value))
				return;

			_selectedItem = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(NotesTitle));
			OnPropertyChanged(nameof(NotesMeta));
			OnPropertyChanged(nameof(NotesMarkdown));
			OnPropertyChanged(nameof(OpenUrl));
			OnPropertyChanged(nameof(HasSelection));
			((RelayCommand)OpenOnGitHubCommand).RaiseCanExecuteChanged();
		}
	}

	public bool HasSelection => _selectedItem != null;

	public string SearchText
	{
		get => _searchText;
		set
		{
			value ??= string.Empty;
			if (_searchText == value)
				return;

			_searchText = value;
			OnPropertyChanged();
			Refill(_selectedItem);
		}
	}

	/// <summary>
	/// A note above the notes: the list could not be loaded, or is not current
	/// </summary>
	public string StatusText
	{
		get => _statusText;
		private set
		{
			if (_statusText == value)
				return;

			_statusText = value;
			OnPropertyChanged();
		}
	}

	public bool IsLoading
	{
		get => _isLoading;
		private set
		{
			if (_isLoading == value)
				return;

			_isLoading = value;
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Shown instead of the list when there is nothing to list
	/// </summary>
	public string EmptyText =>
		Items.Count > 0 || IsLoading ? string.Empty
			: _searchText.Trim().Length > 0
				? string.Format(CultureInfo.CurrentCulture, Text("Changelog_NoResults", "No version found for “{0}”."), _searchText.Trim())
				: Text("Changelog_Empty", "The changelog is not available yet. Check your connection and open it again.");

	public string NotesTitle => _selectedItem?.Title ?? string.Empty;

	/// <summary>
	/// Date of the version, or how many versions the entry gathers
	/// </summary>
	public string NotesMeta => _selectedItem?.Subtitle ?? string.Empty;

	/// <summary>
	/// The notes of the selected line as Markdown: of one version, or of all versions since the one that was installed before
	/// </summary>
	public string NotesMarkdown => _selectedItem == null ? string.Empty : ComposeNotes(_selectedItem);

	/// <summary>
	/// The release page of the selected version on GitHub, or null
	/// </summary>
	public string? OpenUrl => _selectedItem?.Entry?.Url is { Length: > 0 } url ? url : _selectedItem?.Entries.FirstOrDefault()?.Url;

	public ICommand OpenOnGitHubCommand { get; }

	/// <summary>
	/// Shows the list that was saved, then loads the current one from GitHub
	/// </summary>
	/// <param name="selectSince">Whether the entry for everything since the previous version is selected first</param>
	public async Task LoadAsync(bool selectSince)
	{
		IsLoading = true;
		try
		{
			Apply(_changelogService.LoadSaved(), selectSince);
			Apply(await _changelogService.LoadAsync().ConfigureAwait(true), selectSince, keepSelection: true);
		}
		finally
		{
			IsLoading = false;
			OnPropertyChanged(nameof(EmptyText));
		}
	}

	/// <summary>
	/// Shows a list of versions, e.g. the one that was loaded
	/// </summary>
	public void Apply(ChangelogList list, bool selectSince, bool keepSelection = false)
	{
		var includePrereleases = UpdateService.IncludesPrereleases(_configurationService.LoadConfiguration());
		_entries = list.Entries.Where(entry => !entry.IsPrerelease || includePrereleases || entry.Version.CompareTo(_current) == 0).ToList();
		StatusText = list.IsFromCache && list.SavedAt != null
			? string.Format(CultureInfo.CurrentCulture, Text("Changelog_Offline", "GitHub could not be reached. This is the list saved on {0}."), list.SavedAt.Value.LocalDateTime.ToString("g", CultureInfo.CurrentCulture))
			: string.Empty;

		var previousSelection = keepSelection ? _selectedItem : null;
		var items = CreateItems();
		Refill(null, items);
		var wanted = previousSelection != null
			? Items.FirstOrDefault(item => item.Title == previousSelection.Title)
			: null;
		wanted ??= selectSince ? Items.FirstOrDefault(item => item.IsSince) : null;
		wanted ??= Items.FirstOrDefault(item => item.IsInstalled) ?? Items.FirstOrDefault();
		SelectedItem = wanted;
		OnPropertyChanged(nameof(EmptyText));
	}

	private List<ChangelogListItem> CreateItems()
	{
		var items = new List<ChangelogListItem>();
		var installedText = Text("Changelog_Installed", "Installed");
		var newText = Text("Changelog_New", "New");
		var prereleaseText = Text("Changelog_Prerelease", "Prerelease");

		if (_previous != null)
		{
			var since = ChangelogService.VersionsBetween(_entries, _previous, _current);
			if (since.Count > 0)
			{
				items.Add(new ChangelogListItem(
					string.Format(CultureInfo.CurrentCulture, Text("Changelog_Since", "Since {0}"), _previous),
					string.Format(CultureInfo.CurrentCulture, since.Count == 1 ? Text("Changelog_OneVersion", "{0} version") : Text("Changelog_Versions", "{0} versions"), since.Count),
					entries: since)
				{
					NewText = newText
				});
			}
		}

		foreach (var entry in _entries)
		{
			items.Add(new ChangelogListItem(entry.Version.ToString(), FormatDate(entry.PublishedAt), entry)
			{
				IsInstalled = entry.Version.CompareTo(_current) == 0,
				IsNew = _previous != null && entry.Version.CompareTo(_previous) > 0 && entry.Version.CompareTo(_current) <= 0,
				InstalledText = installedText,
				NewText = newText,
				PrereleaseText = prereleaseText
			});
		}

		_items = items;
		return items;
	}

	/// <summary>
	/// Fills the list with the lines that match the search and keeps the selected line when it is still in it
	/// </summary>
	private void Refill(ChangelogListItem? selected, List<ChangelogListItem>? items = null)
	{
		var all = items ?? _items;
		var query = _searchText.Trim();
		var visible = query.Length == 0
			? all
			: all.Where(item => !item.IsSince && Matches(item, query)).ToList();

		Items.Clear();
		foreach (var item in visible)
			Items.Add(item);

		if (selected != null && Items.Contains(selected))
			SelectedItem = selected;
		else if (items == null)
			SelectedItem = Items.FirstOrDefault(item => item.IsInstalled) ?? Items.FirstOrDefault();

		OnPropertyChanged(nameof(EmptyText));
	}

	private static bool Matches(ChangelogListItem item, string query) =>
		item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
		(item.Entry?.Markdown.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);

	private string ComposeNotes(ChangelogListItem item)
	{
		if (!item.IsSince)
			return NotesOf(item.Entry!);

		// Every version below its own heading; the headings of the notes move one level down.
		return string.Join("\n\n", item.Entries.Select(entry =>
			$"## {entry.Version}  ·  {FormatDate(entry.PublishedAt)}\n\n{Demote(NotesOf(entry))}"));
	}

	private string NotesOf(ChangelogEntry entry) =>
		entry.Markdown.Length > 0 ? entry.Markdown : Text("Changelog_NoNotes", "This version has no release notes.");

	private static string Demote(string markdown) =>
		Regex.Replace(markdown, @"^(#{1,5})(?=\s)", "$1#", RegexOptions.Multiline);

	private static string FormatDate(DateTimeOffset? date) =>
		date?.LocalDateTime.ToString("d", CultureInfo.CurrentCulture) ?? string.Empty;

	private void OpenOnGitHub()
	{
		if (OpenUrl != null)
			Process.Start(new ProcessStartInfo(OpenUrl) { UseShellExecute = true });
	}

	private string Text(string key, string defaultValue) => _localizationService.GetString(key, defaultValue);

	public event PropertyChangedEventHandler? PropertyChanged;

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
