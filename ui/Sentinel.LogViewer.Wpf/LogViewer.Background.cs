using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// Big lists: the checks of the filter, the search and the hit counts of the filter input run on a copy of the entries in the background.
    /// The list shows the old result until the new one is there; after a short time a grey card with the progress and a button to cancel
    /// covers the list. The UI thread only takes over the result.
    /// </summary>
    public partial class LogViewer
    {
        // shortcut: fixed number of entries; below it everything runs at once as before. Measure again when the checks get faster or slower.
        internal static int BackgroundThreshold = 10_000;

        /// <summary>How many entries a check handles before it looks at the cancel request; the tests make it small</summary>
        internal static int BackgroundChunk = 2048;

        /// <summary>Called on the worker thread after each chunk; the tests hold a check here</summary>
        internal static Action? BackgroundChunkHook;

        private static readonly TimeSpan BusyDelay = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan CheckPause = TimeSpan.FromMilliseconds(250);

        private enum JobKind
        {
            Filter,
            Find,
            Hits
        }

        private sealed class Job
        {
            public Job(string textKey, int total, bool quiet)
            {
                TextKey = textKey;
                Total = total;
                Quiet = quiet;
            }

            /// <summary>A check that new entries asked for, not the user: it never shows the card</summary>
            public bool Quiet { get; }

            public string TextKey { get; }
            public int Total { get; }
            public CancellationTokenSource Cancellation { get; } = new();
            public Stopwatch Running { get; } = Stopwatch.StartNew();
            public volatile int Done;
            public volatile int Hits;
        }

        private readonly Job?[] _jobs = new Job?[3];
        private readonly bool[] _jobsDirty = new bool[3];
        private readonly bool[] _requestQuiet = new bool[3];
        private bool _recounting;
        private readonly DispatcherTimer?[] _jobPauses = new DispatcherTimer?[3];
        private readonly Action?[] _jobStarts = new Action?[3];
        private DispatcherTimer? _busyTimer;
        private ICommand? _cancelBusyCommand;

        // The verdict of the filter for each entry, made in the background; entries that arrive later are judged when the view asks.
        private Dictionary<LogEntry, bool>? _verdicts;

        #region Busy

        private static readonly DependencyPropertyKey IsBusyPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsBusy), typeof(bool), typeof(LogViewer), new PropertyMetadata(false));

        /// <summary>
        /// The <see cref="IsBusy"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty IsBusyProperty = IsBusyPropertyKey.DependencyProperty;

        /// <summary>
        /// Whether a check of a big list runs for so long that the list is covered by the card with the progress
        /// </summary>
        [Category("LogViewer")]
        public bool IsBusy => (bool)GetValue(IsBusyProperty);

        private static readonly DependencyPropertyKey BusyTextPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(BusyText), typeof(string), typeof(LogViewer), new PropertyMetadata(string.Empty));

        /// <summary>
        /// The <see cref="BusyText"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty BusyTextProperty = BusyTextPropertyKey.DependencyProperty;

        /// <summary>
        /// What the check does, e.g. "Filtering the entries"
        /// </summary>
        [Category("LogViewer")]
        public string BusyText => (string)GetValue(BusyTextProperty);

        private static readonly DependencyPropertyKey BusyDetailPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(BusyDetail), typeof(string), typeof(LogViewer), new PropertyMetadata(string.Empty));

        /// <summary>
        /// The <see cref="BusyDetail"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty BusyDetailProperty = BusyDetailPropertyKey.DependencyProperty;

        /// <summary>
        /// How far the check is and what it found so far, e.g. "41,000 of 100,000 · 12 hits so far"
        /// </summary>
        [Category("LogViewer")]
        public string BusyDetail => (string)GetValue(BusyDetailProperty);

        private static readonly DependencyPropertyKey BusyProgressPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(BusyProgress), typeof(double), typeof(LogViewer), new PropertyMetadata(0.0));

        /// <summary>
        /// The <see cref="BusyProgress"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty BusyProgressProperty = BusyProgressPropertyKey.DependencyProperty;

        /// <summary>
        /// How far the check is, from 0 to 1
        /// </summary>
        [Category("LogViewer")]
        public double BusyProgress => (double)GetValue(BusyProgressProperty);

        /// <summary>
        /// Stops the running checks; the list keeps the result that it showed before (Esc while the card is shown)
        /// </summary>
        public ICommand CancelBusyCommand => _cancelBusyCommand ??= new RelayCommand(CancelAllJobs);

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape && IsBusy)
            {
                CancelAllJobs();
                e.Handled = true;
                return;
            }

            base.OnPreviewKeyDown(e);
        }

        #endregion

        #region Jobs

        /// <summary>
        /// Runs <paramref name="work"/> on the thread pool and gives its result to <paramref name="apply"/> on the UI thread, unless the job was
        /// cancelled or replaced meanwhile. A job of the same kind that still runs is cancelled.
        /// </summary>
        private void StartJob<T>(JobKind kind, string textKey, int total, Func<Job, T> work, Action<T> apply)
        {
            CancelJob(kind);
            var job = new Job(textKey, total, _requestQuiet[(int)kind]);
            _jobs[(int)kind] = job;
            EnsureBusyTimer();

            Task.Run(() => work(job), job.Cancellation.Token).ContinueWith(task => Dispatcher.InvokeAsync(() =>
            {
                if (!ReferenceEquals(_jobs[(int)kind], job))
                    return;

                _jobs[(int)kind] = null;
                // shortcut: a check that fails (it should not, the matching handles its own time-outs) leaves the old result; when to upgrade: if it ever happens in the field
                if (task.IsCompletedSuccessfully)
                    apply(task.Result);

                UpdateBusy();
                if (_jobsDirty[(int)kind])
                {
                    _jobsDirty[(int)kind] = false;
                    RequestJob(kind, _requestQuiet[(int)kind]);
                }
            }), TaskScheduler.Default);
        }

        private void CancelJob(JobKind kind)
        {
            _jobPauses[(int)kind]?.Stop();
            _jobsDirty[(int)kind] = false;
            _jobs[(int)kind]?.Cancellation.Cancel();
            _jobs[(int)kind] = null;
            UpdateBusy();
        }

        private void CancelAllJobs()
        {
            foreach (var kind in Enum.GetValues<JobKind>())
                CancelJob(kind);
        }

        /// <summary>
        /// Asks for a check with the current state: after a short pause, so that typing and a stream of new entries do not start one check after
        /// the other, and not while the same kind still runs (it starts again when it is done)
        /// </summary>
        private void RequestJob(JobKind kind, bool quiet)
        {
            // Several requests make one check; it is quiet only when all of them are.
            var pending = _jobsDirty[(int)kind] || (_jobPauses[(int)kind]?.IsEnabled ?? false);
            _requestQuiet[(int)kind] = quiet && (!pending || _requestQuiet[(int)kind]);

            if (_jobs[(int)kind] != null)
            {
                _jobsDirty[(int)kind] = true;
                return;
            }

            var pause = _jobPauses[(int)kind] ??= CreatePause(kind);
            if (!pause.IsEnabled)
                pause.Start();
        }

        private DispatcherTimer CreatePause(JobKind kind)
        {
            var pause = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = CheckPause };
            pause.Tick += (_, _) =>
            {
                pause.Stop();
                _jobStarts[(int)kind]?.Invoke();
            };
            return pause;
        }

        /// <summary>
        /// For a big list asks for the check to run in the background and returns true; for a small list stops a running one and returns false,
        /// and the caller checks at once
        /// </summary>
        private bool UseBackground(JobKind kind, int count, Action start)
        {
            if (count < BackgroundThreshold)
            {
                CancelJob(kind);
                return false;
            }

            _jobStarts[(int)kind] = start;
            RequestJob(kind, _recounting);
            return true;
        }

        private void EnsureBusyTimer()
        {
            _busyTimer ??= new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(100) };
            _busyTimer.Tick -= OnBusyTick;
            _busyTimer.Tick += OnBusyTick;
            _busyTimer.Start();
        }

        private void OnBusyTick(object? sender, EventArgs e) => UpdateBusy();

        /// <summary>
        /// Shows or hides the card and brings its numbers up to date
        /// </summary>
        private void UpdateBusy()
        {
            var running = _jobs.Where(job => job != null).Cast<Job>().ToList();
            if (running.Count == 0)
                _busyTimer?.Stop();

            // Checks that new entries asked for stay quiet: the list is not covered while the user did nothing.
            var asked = running.Where(job => !job.Quiet).ToList();
            if (asked.Count == 0)
            {
                SetValue(IsBusyPropertyKey, false);
                return;
            }

            var main = asked[0];
            SetValue(IsBusyPropertyKey, asked.Any(job => job.Running.Elapsed >= BusyDelay));
            SetValue(BusyTextPropertyKey, LogViewerTexts.Get(main.TextKey));
            SetValue(BusyProgressPropertyKey, main.Total == 0 ? 0 : Math.Min(1.0, (double)main.Done / main.Total));
            var detail = LogViewerTexts.Format("Busy_Progress", Math.Min(main.Done, main.Total), main.Total);
            var hits = main.Hits;
            SetValue(BusyDetailPropertyKey, hits > 0 ? detail + " · " + LogViewerTexts.Format("Busy_Hits", hits) : detail);
        }

        /// <summary>
        /// Goes through the entries in chunks: asks <paramref name="matches"/> for each one, counts the hits for the card and looks at the
        /// cancel request after each chunk
        /// </summary>
        private static List<bool> Check(IReadOnlyList<LogEntry> entries, Job job, Func<LogEntry, bool> matches)
        {
            var results = new List<bool>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                var hit = matches(entries[i]);
                results.Add(hit);
                if (hit)
                    job.Hits++;

                if ((i + 1) % BackgroundChunk == 0 || i == entries.Count - 1)
                {
                    job.Done = i + 1;
                    BackgroundChunkHook?.Invoke();
                    job.Cancellation.Token.ThrowIfCancellationRequested();
                }
            }

            return results;
        }

        #endregion

        #region Filter

        /// <summary>
        /// Whether an entry passes the filter: logger rules, time range, hidden levels and the filter terms, taken as they are now. The same
        /// test runs on the UI thread for small lists and in the background for big ones, so the result is the same.
        /// </summary>
        private Func<LogEntry, bool> BuildFilterTest(LoggerVisibilityRules rules)
        {
            var timeFrom = TimeFrom;
            var timeTo = TimeTo;
            var fileZone = FileTimeZone;
            var hiddenLevels = new[] { TraceFilter, DebugFilter, InfoFilter, WarnFilter, ErrorFilter, FatalFilter };
            var terms = ActiveSearchTerms?.ToArray() ?? Array.Empty<FilterTerm>();
            var fields = SearchFields;

            return logEvent =>
            {
                if (!rules.IsVisible(logEvent.LoggerName))
                    return false;

                // The time range: only the entries of the period, a time without a zone read in the zone of the file.
                if ((timeFrom != null || timeTo != null) && !EntryTime.IsInRange(logEvent.TimeStamp, fileZone, timeFrom, timeTo))
                    return false;

                // A level that is switched on is hidden.
                if ((int)logEvent.Level is >= 0 and < 6 && hiddenLevels[(int)logEvent.Level])
                    return false;

                // The filter terms: every term that includes must match, and no term that excludes may match.
                foreach (var term in terms)
                {
                    if (term.IsEnabled && term.Matches(logEvent, fields) == term.IsExclude)
                        return false;
                }

                return true;
            };
        }

        /// <summary>Whether any filter is on, so that a check has something to do</summary>
        private bool HasFilterCriteria() =>
            TimeFrom != null || TimeTo != null || LoggerTree.Rules.HasRules || LevelNames.Any(IsLevelHidden) || (ActiveSearchTerms?.Any(term => term.IsEnabled) ?? false);

        /// <summary>
        /// Starts the check of all entries in the background; the view gets the filter when it is done
        /// </summary>
        private void StartFilterJob(CollectionView view, ListView? listView)
        {
            var entries = GetEffectiveEntries().ToArray();
            var rules = new LoggerVisibilityRules();
            rules.Load(LoggerTree.Rules.ToList());
            var inBackground = BuildFilterTest(rules);
            var onUi = BuildFilterTest(LoggerTree.Rules);

            StartJob(JobKind.Filter, "Busy_Filtering", entries.Length,
                job => Check(entries, job, inBackground),
                results =>
                {
                    var verdicts = new Dictionary<LogEntry, bool>(entries.Length);
                    for (var i = 0; i < entries.Length; i++)
                        verdicts[entries[i]] = results[i];

                    _verdicts = verdicts;
                    ApplyFilter(view, listView, item => item is not LogEntry entry || VerdictOf(entry, onUi));
                });
        }

        /// <summary>How many entries the filter has a verdict for; the tests check that it follows the list</summary>
        internal int VerdictCount => _verdicts?.Count ?? 0;

        private bool VerdictOf(LogEntry entry, Func<LogEntry, bool> test)
        {
            var verdicts = _verdicts;
            if (verdicts == null)
                return test(entry);

            if (!verdicts.TryGetValue(entry, out var verdict))
                verdicts[entry] = verdict = test(entry);

            return verdict;
        }

        /// <summary>
        /// Entries that left the list leave the verdicts as well
        /// </summary>
        private void ForgetVerdicts(NotifyCollectionChangedEventArgs e)
        {
            if (_verdicts == null)
                return;

            if (e.Action is NotifyCollectionChangedAction.Reset)
                _verdicts.Clear();
            else if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace && e.OldItems != null)
                foreach (LogEntry old in e.OldItems)
                    _verdicts.Remove(old);
        }

        #endregion

        #region Hits and search

        private void StartHitsJob()
        {
            if (LogEvents?.View == null || string.IsNullOrEmpty(CurrentSearchText) || FilterError != null)
                return;

            var entries = LogEvents.View.OfType<LogEntry>().ToArray();
            var term = new FilterTerm(CurrentSearchText, FilterIsExclude, FilterMatchCase, FilterWholeWord, UseRegexSearch, FilterLevel);
            var fields = SearchFields;

            StartJob(JobKind.Hits, "Busy_Filtering", entries.Length,
                job => Check(entries, job, entry => term.Matches(entry, fields)),
                results => ShowFilterHits(results.Count(hit => hit)));
        }

        private void StartFindJob()
        {
            var term = _findTerm;
            if (term == null || LogEvents?.View == null)
                return;

            var entries = LogEvents.View.OfType<LogEntry>().ToArray();
            var fields = SearchFields;

            StartJob(JobKind.Find, "Busy_Searching", entries.Length,
                job => Check(entries, job, entry => term.Matches(entry, fields)),
                results =>
                {
                    _findMatches.Clear();
                    _findMatchIndices.Clear();
                    for (var i = 0; i < entries.Length; i++)
                    {
                        if (!results[i])
                            continue;

                        _findMatches.Add(entries[i]);
                        _findMatchIndices.Add(i);
                    }

                    if (_currentFindMatch != null && !_findMatches.Contains(_currentFindMatch))
                        _currentFindMatch = null;

                    UpdateFindResultText();
                });
        }

        #endregion
    }
}
