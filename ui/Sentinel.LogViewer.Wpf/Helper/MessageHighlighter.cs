using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Sentinel.LogViewer.Wpf.Resolver;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// Attached behavior to render a TextBlock with highlighted segments based on ActiveSearchTerms.
    /// It uses an ancestor <see cref="LogViewerBase"/> to access resolvers and highlight brush.
    /// </summary>
    public static class MessageHighlighter
    {
        public static readonly DependencyProperty ResolverProperty = DependencyProperty.RegisterAttached(
            "Resolver",
            typeof(ILogEntryResolver),
            typeof(MessageHighlighter),
            new PropertyMetadata(null, OnResolverChanged));

        public static void SetResolver(DependencyObject element, ILogEntryResolver value) => element.SetValue(ResolverProperty, value);
        public static ILogEntryResolver GetResolver(DependencyObject element) => (ILogEntryResolver)element.GetValue(ResolverProperty);

        private static void OnResolverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock textBlock)
            {
                if (e.NewValue != null)
                {
                    textBlock.Loaded += TextBlockOnLoaded;
                    textBlock.Unloaded += TextBlockOnUnloaded;
                    TryUpdate(textBlock);
                }
                else
                {
                    textBlock.Loaded -= TextBlockOnLoaded;
                    textBlock.Unloaded -= TextBlockOnUnloaded;
                }
            }
        }

        private static void TextBlockOnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock tb)
                TryUpdate(tb);
        }

        private static void TextBlockOnUnloaded(object sender, RoutedEventArgs e)
        {
            // no-op
        }

        /// <summary>
        /// Updates the highlighting of all realized text blocks below <paramref name="root"/>, e.g. after the search text changed
        /// </summary>
        internal static void Refresh(DependencyObject root)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is TextBlock textBlock && GetResolver(textBlock) != null)
                    TryUpdate(textBlock);
                else
                    Refresh(child);
            }
        }

        /// <summary>
        /// Rebuilds the Inlines of the TextBlock, highlighting matches of the ActiveSearchTerms and of the search text.
        /// </summary>
        private static void TryUpdate(TextBlock textBlock)
        {
            var logViewer = FindAncestor<LogViewerBase>(textBlock);
            if (logViewer == null)
                return;

            var logEntry = textBlock.DataContext as Sentinel.LogViewer.Core.LogEntry;
            if (logEntry == null)
                return;

            var resolver = GetResolver(textBlock);
            if (resolver == null)
                return;

            string message = resolver.Resolve(logEntry);
            var terms = logViewer.ActiveSearchTerms;
            var findText = logViewer.FindText;
            if (string.IsNullOrEmpty(message) || ((terms == null || terms.Count == 0) && string.IsNullOrEmpty(findText)))
            {
                textBlock.Text = message; // fallback
                return;
            }

            // Highlight per character; search hits are painted over filter matches.
            var highlights = new Brush?[message.Length];
            var highlighted = false;
            foreach (var term in terms ?? Enumerable.Empty<SearchTerm>())
            {
                foreach (var (start, length) in FindMatches(message, term))
                {
                    Array.Fill(highlights, logViewer.SearchHighlightBackground, start, length);
                    highlighted = true;
                }
            }

            if (!string.IsNullOrEmpty(findText))
            {
                foreach (var (start, length) in FindMatches(message, new TextSearchTerm(findText)))
                {
                    Array.Fill(highlights, logViewer.FindHighlightBackground, start, length);
                    highlighted = true;
                }
            }

            if (!highlighted)
            {
                textBlock.Text = message;
                return;
            }

            // Rebuild inlines, one run per stretch of equal highlighting
            textBlock.Inlines.Clear();
            var runStart = 0;
            for (var i = 1; i <= message.Length; i++)
            {
                if (i < message.Length && ReferenceEquals(highlights[i], highlights[runStart]))
                    continue;

                textBlock.Inlines.Add(new Run(message.Substring(runStart, i - runStart)) { Background = highlights[runStart] });
                runStart = i;
            }
        }

        private static IEnumerable<(int start, int length)> FindMatches(string text, SearchTerm term)
        {
            if (term is RegexSearchTerm regexTerm)
            {
                foreach (Match m in Regex.Matches(text, regexTerm.Text))
                {
                    if (m.Success && m.Length > 0)
                        yield return (m.Index, m.Length);
                }
            }
            else if (term is TextSearchTerm simple)
            {
                string needle = simple.ToString();
                if (string.IsNullOrEmpty(needle)) yield break;
                int pos = 0;
                var comparison = StringComparison.InvariantCultureIgnoreCase;
                while ((pos = text.IndexOf(needle, pos, comparison)) >= 0)
                {
                    yield return (pos, needle.Length);
                    pos += needle.Length;
                }
            }
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T typed) return typed;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return default;
        }
    }
}


