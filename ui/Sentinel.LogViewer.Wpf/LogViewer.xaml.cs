using System;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Threading;
using NLog;
using Sentinel.LogViewer.Wpf.Targets;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// Log viewer that subscribes to NLog <see cref="CacheTarget"/> via configuration or the <see cref="CacheTarget"/> property.
    /// </summary>
    public partial class LogViewer : LogViewerBase
    {
        private IDisposable? _subscription;
        private bool _isListening;

        /// <summary>
        /// Looks up a target with this name in the NLog configuration and links it via <see cref="CacheTarget.GetInstance"/>.
        /// </summary>
        [Category("LogViewer")]
        public string TargetName
        {
            get => (string)GetValue(TargetNameProperty);
            set => SetValue(TargetNameProperty, value);
        }

        /// <summary>
        /// The <see cref="TargetName"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty TargetNameProperty =
            DependencyProperty.Register(nameof(TargetName), typeof(string), typeof(LogViewer), new PropertyMetadata(null));

        /// <summary>
        /// Cache target to subscribe to. When set, <see cref="StartListen"/> is invoked with this instance.
        /// </summary>
        [Category("LogViewer")]
        [Browsable(true)]
        [Description("Cache target for log events. When set, the control subscribes via StartListen.")]
        public ICacheTarget? CacheTarget
        {
            get => (ICacheTarget?)GetValue(CacheTargetProperty);
            set => SetValue(CacheTargetProperty, value);
        }

        /// <summary>
        /// The <see cref="CacheTarget"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty CacheTargetProperty = DependencyProperty.Register(
            nameof(CacheTarget),
            typeof(ICacheTarget),
            typeof(LogViewer),
            new PropertyMetadata(null, OnCacheTargetChanged));

        private static void OnCacheTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LogViewer instance && e.NewValue is ICacheTarget target && target != null)
                instance.StartListen(target);
        }

        static LogViewer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(LogViewer),
                new FrameworkPropertyMetadata(typeof(LogViewerBase)));
        }

        /// <summary>
        /// Initializes a viewer wired for NLog <see cref="CacheTarget"/> subscriptions.
        /// </summary>
        public LogViewer()
        {
        }

        /// <summary>
        /// Starts listening by subscribing to the cache observable (explicit target or <see cref="CacheTarget.GetInstance"/>).
        /// </summary>
        public void StartListen(ICacheTarget? target = null)
        {
            if (_isListening || DesignerProperties.GetIsInDesignMode(this))
                return;

            var dispatcher = Window.GetWindow(this)?.Dispatcher
                ?? Application.Current?.Dispatcher
                ?? Dispatcher.CurrentDispatcher;
            if (dispatcher == null)
                return;

            target ??= global::Sentinel.LogViewer.Wpf.Targets.CacheTarget.GetInstance(targetName: TargetName);

            _subscription = target.Cache.SubscribeOn(Scheduler.Default)
                .Buffer(TimeSpan.FromMilliseconds(100))
                .Where(x => x.Any())
                .ObserveOn(new DispatcherSynchronizationContext(dispatcher))
                .Subscribe(AppendLogEntriesBatch);

            _isListening = true;
        }

        /// <summary>
        /// Stops listening by disposing the cache subscription.
        /// </summary>
        public void StopListen()
        {
            if (!_isListening)
                return;

            _subscription?.Dispose();
            _subscription = null;
            _isListening = false;
        }

        /// <inheritdoc />
        protected override void OnPausePropertyChanged(bool isPaused)
        {
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            if (isPaused)
                StopListen();
            else
                StartListen(CacheTarget);
        }

        /// <inheritdoc />
        protected override void OnViewerLoaded()
        {
            StartListen();
        }

        /// <inheritdoc />
        protected override void DisposeViewerResources()
        {
            StopListen();
        }
    }
}
