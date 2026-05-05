namespace Sentinel.NLogViewer.App.Models;

/// <summary>
/// User-chosen destination for importing one or more log files in a batch.
/// </summary>
public sealed class LogFileImportBatchContext
{
	/// <summary>
	/// How parsed log entries should be routed to tabs.
	/// </summary>
	public enum DestinationMode
	{
		PerFileNewTab,
		SingleNewTab,
		ExistingTab
	}

	public LogFileImportBatchContext(DestinationMode mode, LogTabViewModel? existingTargetTab,
		string? sharedTabHeaderWhenSingleNewTab)
	{
		Mode = mode;
		ExistingTargetTab = existingTargetTab;
		SharedTabHeaderWhenSingleNewTab = sharedTabHeaderWhenSingleNewTab;
	}

	public DestinationMode Mode { get; }

	public LogTabViewModel? ExistingTargetTab { get; }

	/// <summary>
	/// Display name for <see cref="DestinationMode.SingleNewTab"/> before the shared tab exists.
	/// </summary>
	public string? SharedTabHeaderWhenSingleNewTab { get; }

	/// <summary>
	/// Lazily assigned when the first parsed file is applied in <see cref="DestinationMode.SingleNewTab"/>.
	/// </summary>
	public LogTabViewModel? SharedNewTab { get; set; }
}
