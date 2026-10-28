using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// A text of the control in XAML, in the language of <see cref="LogViewerTexts"/>: <c>{local:Text Key=Filter_Name}</c>. It is a binding, so
    /// the text changes at once when the language does. A text with an argument is <c>{local:Text Key=Chip_ApplyName, Arg0={Binding Text}}</c>
    /// (a value) or <c>{local:Text Key=Level_HideTip, Arg0Key=Level_Warn}</c> (another text).
    /// </summary>
    [MarkupExtensionReturnType(typeof(object))]
    public sealed class TextExtension : MarkupExtension
    {
        public TextExtension()
        {
        }

        public TextExtension(string key)
        {
            Key = key;
        }

        /// <summary>The key of the text</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>The value for <c>{0}</c> of the text</summary>
        public BindingBase? Arg0 { get; set; }

        /// <summary>The key of another text that is the value for <c>{0}</c></summary>
        public string? Arg0Key { get; set; }

        /// <summary>The key of the text as a binding, e.g. <c>{local:Text KeyOf={Binding Key}}</c> for the items of a list</summary>
        public BindingBase? KeyOf { get; set; }

        /// <summary>Put before the bound value to make the key, e.g. <c>KeyPrefix=Level_</c> turns the level <c>Warn</c> into <c>Level_Warn</c></summary>
        public string KeyPrefix { get; set; } = string.Empty;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (KeyOf != null)
            {
                var keyed = new MultiBinding { Converter = new KeyConverter(KeyPrefix), Mode = BindingMode.OneWay };
                keyed.Bindings.Add(new Binding(nameof(TextSource.Language)) { Source = LogViewerTexts.Source, Mode = BindingMode.OneWay });
                keyed.Bindings.Add(KeyOf);
                return keyed.ProvideValue(serviceProvider);
            }

            if (Arg0 == null && Arg0Key == null)
                return new Binding($"[{Key}]") { Source = LogViewerTexts.Source, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);

            var multi = new MultiBinding { Converter = new FormatConverter(Key, Arg0Key), Mode = BindingMode.OneWay };
            multi.Bindings.Add(new Binding(nameof(TextSource.Language)) { Source = LogViewerTexts.Source, Mode = BindingMode.OneWay });
            if (Arg0 != null)
                multi.Bindings.Add(Arg0);

            return multi.ProvideValue(serviceProvider);
        }

        private sealed class KeyConverter : IMultiValueConverter
        {
            private readonly string _prefix;

            public KeyConverter(string prefix)
            {
                _prefix = prefix;
            }

            public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
                values.Length > 1 && values[1] is { } key && key != System.Windows.DependencyProperty.UnsetValue ? LogViewerTexts.Get(_prefix + key) : string.Empty;

            public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) => throw new NotSupportedException();
        }

        private sealed class FormatConverter : IMultiValueConverter
        {
            private readonly string _key;
            private readonly string? _argumentKey;

            public FormatConverter(string key, string? argumentKey)
            {
                _key = key;
                _argumentKey = argumentKey;
            }

            public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
            {
                var argument = _argumentKey != null ? LogViewerTexts.Get(_argumentKey) : values.Length > 1 ? values[1] : null;
                return LogViewerTexts.Format(_key, argument == System.Windows.DependencyProperty.UnsetValue ? string.Empty : argument);
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) => throw new NotSupportedException();
        }
    }
}