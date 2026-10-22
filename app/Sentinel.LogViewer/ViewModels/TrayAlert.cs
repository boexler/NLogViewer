using System;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Models;

namespace Sentinel.LogViewer.ViewModels;

/// <summary>
/// Entries that are worth a notification in the tray: the most important one of a batch of one application and how many there were
/// </summary>
/// <param name="Tab">The tab that shows the entries, which a click on the notification selects; held weakly, so that a tab that is closed can be freed with its entries</param>
public sealed record TrayAlert(WeakReference<LogTabViewModel> Tab, string AppName, LogEntry Entry, int Count);
