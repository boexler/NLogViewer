using Sentinel.LogViewer.Core;
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
    /// It uses an ancestor <see cref="LogViewer"/> to access resolvers and highlight brush.
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
            var logViewer = FindAncestor<LogViewer>(textBlock);
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
            var findTerm = logViewer.FindTerm;
            if (string.IsNullOrEmpty(message) || ((terms == null || terms.Count == 0) && findTerm == null))
            {
                textBlock.Text = message; // fallback
                return;
            }

            // Highlight per character; search hits are painted over filter matches.
            var highlights = new Brush?[message.Length];
            var highlighted = false;
            // A term that excludes hides the entries it matches, so there is nothing of it to see.
            foreach (var term in (terms ?? Enumerable.Empty<FilterTerm>()).Where(term => !term.IsExclude))
            {
                foreach (var (start, length) in term.FindMatches(message))
                {
                    Array.Fill(highlights, logViewer.SearchHighlightBackground, start, length);
                    highlighted = true;
                }
            }

            if (findTerm != null)
            {
                foreach (var (start, length) in findTerm.FindMatches(message))
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


