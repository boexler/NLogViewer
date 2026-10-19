using Sentinel.LogViewer.Core;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Sentinel.LogViewer.Wpf.Models;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// Format of the time stamp column: a default for all viewers and a choice of this viewer, made in the context menu of the column
    /// </summary>
    public partial class LogViewer
    {
        private ICommand? _setTimeStampFormatCommand;
        private ICommand? _resetTimeStampFormatCommand;

        #region Time Stamp Format Properties

        /// <summary>
        /// The format of the time stamp column as long as this viewer has none of its own; "Default view" in the context menu goes back to it
        /// </summary>
        [Category("LogViewerColumns")]
        [Description("The format of the time stamp column as long as the viewer has none of its own")]
        public TimeStampFormat DefaultTimeStampFormat
        {
            get => (TimeStampFormat)GetValue(DefaultTimeStampFormatProperty);
            set => SetValue(DefaultTimeStampFormatProperty, value);
        }

        /// <summary>
        /// The <see cref="DefaultTimeStampFormat"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty DefaultTimeStampFormatProperty =
            DependencyProperty.Register(nameof(DefaultTimeStampFormat), typeof(TimeStampFormat), typeof(LogViewer),
                new PropertyMetadata(TimeStampFormat.System, OnTimeStampFormatChanged));

        /// <summary>
        /// The format that this viewer shows instead of the default, or null to follow the default
        /// </summary>
        [Category("LogViewerColumns")]
        [Description("The format that this viewer shows instead of the default, or null to follow the default")]
        public TimeStampFormat? CustomTimeStampFormat
        {
            get => (TimeStampFormat?)GetValue(CustomTimeStampFormatProperty);
            set => SetValue(CustomTimeStampFormatProperty, value);
        }

        /// <summary>
        /// The <see cref="CustomTimeStampFormat"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty CustomTimeStampFormatProperty =
            DependencyProperty.Register(nameof(CustomTimeStampFormat), typeof(TimeStampFormat?), typeof(LogViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTimeStampFormatChanged));

        private static readonly DependencyPropertyKey EffectiveTimeStampFormatPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(EffectiveTimeStampFormat), typeof(TimeStampFormat), typeof(LogViewer),
                new PropertyMetadata(TimeStampFormat.System));

        /// <summary>
        /// The <see cref="EffectiveTimeStampFormat"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty EffectiveTimeStampFormatProperty = EffectiveTimeStampFormatPropertyKey.DependencyProperty;

        /// <summary>
        /// The format that the time stamp column shows: the own choice of this viewer, otherwise the default
        /// </summary>
        public TimeStampFormat EffectiveTimeStampFormat => (TimeStampFormat)GetValue(EffectiveTimeStampFormatProperty);

        private static readonly DependencyPropertyKey CanChangeTimeStampFormatPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(CanChangeTimeStampFormat), typeof(bool), typeof(LogViewer),
                new PropertyMetadata(true));

        /// <summary>
        /// The <see cref="CanChangeTimeStampFormat"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty CanChangeTimeStampFormatProperty = CanChangeTimeStampFormatPropertyKey.DependencyProperty;

        /// <summary>
        /// Whether the format can be changed: only while the built-in <see cref="Resolver.TimeStampResolver"/> is used, not a resolver of your own
        /// </summary>
        public bool CanChangeTimeStampFormat => (bool)GetValue(CanChangeTimeStampFormatProperty);

        #endregion

        /// <summary>
        /// Shows the time stamps of this viewer in the format that is the parameter
        /// </summary>
        public ICommand SetTimeStampFormatCommand => _setTimeStampFormatCommand ??=
            new RelayCommand<TimeStampFormat>(format => CustomTimeStampFormat = format, _ => CanChangeTimeStampFormat);

        /// <summary>
        /// Goes back to the default format
        /// </summary>
        public ICommand ResetTimeStampFormatCommand => _resetTimeStampFormatCommand ??=
            new RelayCommand(() => CustomTimeStampFormat = null, () => CanChangeTimeStampFormat && CustomTimeStampFormat != null);

        private static void OnTimeStampFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as LogViewer)?.UpdateTimeStampFormat();
            (d as LogViewer)?.PublishFilterState();
        }

        private static void OnTimeStampResolverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            d.SetValue(CanChangeTimeStampFormatPropertyKey, e.NewValue is Resolver.TimeStampResolver);
        }

        private void UpdateTimeStampFormat()
        {
            var format = CustomTimeStampFormat ?? DefaultTimeStampFormat;
            SetValue(EffectiveTimeStampFormatPropertyKey, format);

            // The column shows what the resolver returns, so it gets a resolver of the format; a resolver of your own is left alone.
            if (TimeStampResolver is Resolver.TimeStampResolver { } current && current.Format != format)
            {
                TimeStampResolver = new Resolver.TimeStampResolver(format);
                _columnWidthFitter?.Reset(GetEffectiveEntries());
            }
        }
    }
}
