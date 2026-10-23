using System;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// Where the viewer gets its entries: a collection of the caller (<see cref="ItemsSource"/>), a <see cref="Source"/>, or
    /// <see cref="LogBuffer.Default"/>, which is what a viewer without any setting uses
    /// </summary>
    public partial class LogViewer
    {
        private IDisposable? _subscription;
        private bool _isListening;
        private bool _columnsInitialized;

        /// <summary>
        /// The source of the entries. Without it, and without an <see cref="ItemsSource"/>, the viewer shows <see cref="LogBuffer.Default"/>,
        /// which is what <c>AddLogViewer()</c> of the logging of .NET writes to.
        /// The viewer subscribes while it is loaded and not paused.
        /// </summary>
        [Category("LogViewer")]
        [Browsable(true)]
        [Description("The source of the entries. Without one the viewer shows LogBuffer.Default.")]
        public ILogSource? Source
        {
            get => (ILogSource?)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        /// <summary>
        /// The <see cref="Source"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(ILogSource),
            typeof(LogViewer),
            new PropertyMetadata(null, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LogViewer { IsLoaded: true } viewer && !DesignerProperties.GetIsInDesignMode(viewer))
            {
                viewer.StopListen();
                if (!viewer.Pause)
                    viewer.StartListen();
            }
        }

        /// <summary>
        /// Starts listening to the entries of <paramref name="source"/> (by default the <see cref="Source"/> of the viewer, or <see cref="LogBuffer.Default"/>).
        /// Nothing happens when the viewer listens already, or when the entries come from an <see cref="ItemsSource"/>.
        /// </summary>
        public void StartListen(ILogSource? source = null)
        {
            if (_isListening || ItemsSource != null || DesignerProperties.GetIsInDesignMode(this))
                return;

            var dispatcher = Window.GetWindow(this)?.Dispatcher
                ?? Application.Current?.Dispatcher
                ?? Dispatcher.CurrentDispatcher;
            if (dispatcher == null)
                return;

            source ??= Source ?? LogBuffer.Default;

            // A source with a cache sends everything it holds to a new subscriber, so what is shown from an earlier subscription goes first.
            _internalLogEntries.Clear();

            _subscription = source.Entries.SubscribeOn(Scheduler.Default)
                .Buffer(TimeSpan.FromMilliseconds(100))
                .Where(x => x.Any())
                .ObserveOn(new DispatcherSynchronizationContext(dispatcher))
                .Subscribe(AppendLogEntriesBatch);

            _isListening = true;
        }

        /// <summary>
        /// Stops listening by disposing the subscription.
        /// </summary>
        public void StopListen()
        {
            if (!_isListening)
                return;

            _subscription?.Dispose();
            _subscription = null;
            _isListening = false;
        }

        /// <summary>
        /// Pausing stops the subscription, going on starts it again
        /// </summary>
        private void OnPausePropertyChanged(bool isPaused)
        {
            if (isPaused)
                StopListen();
            else if (IsLoaded)
                StartListen();
        }

        /// <summary>
        /// Called every time the viewer is loaded, e.g. when it comes back after a change of the tab; the setup of the columns runs only once
        /// </summary>
        private void OnViewerLoaded()
        {
            if (!Pause)
                StartListen();
        }

        /// <summary>
        /// Releases the subscription when the viewer is gone from its window
        /// </summary>
        private void DisposeViewerResources()
        {
            StopListen();
        }
    }
}
