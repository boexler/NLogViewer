using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// The keys of the columns of the log list, and what is shown in them. A layout is an ordered list of keys; the control shows the columns
    /// of the layout in that order.
    /// </summary>
    public static class LogColumns
    {
        public const string Id = "id";
        public const string Level = "level";
        public const string TimeStamp = "timestamp";
        public const string Logger = "logger";
        public const string Thread = "thread";
        public const string Message = "message";

        /// <summary>The key of a property of the entries is this prefix and its name, e.g. <c>property:Class</c></summary>
        public const string PropertyPrefix = "property:";

        /// <summary>The columns that the list has; the thread is off by default</summary>
        public static IReadOnlyList<string> Default { get; } = new[] { Id, Level, TimeStamp, Logger, Message };

        /// <summary>The columns of the control itself, in the order in which they are placed when they are shown again</summary>
        public static IReadOnlyList<string> BuiltIn { get; } = new[] { Id, Level, TimeStamp, Logger, Thread, Message };

        public static string Property(string name) => PropertyPrefix + name;

        public static bool IsProperty(string key, out string name)
        {
            var isProperty = key.StartsWith(PropertyPrefix, StringComparison.Ordinal) && key.Length > PropertyPrefix.Length;
            name = isProperty ? key[PropertyPrefix.Length..] : string.Empty;
            return isProperty;
        }

        /// <summary>
        /// The layout without duplicates and without keys that are not columns; the message is always there, at the end if it is missing
        /// </summary>
        public static List<string> Normalize(IEnumerable<string>? keys)
        {
            var result = new List<string>();
            foreach (var key in keys ?? Default)
            {
                if (!result.Contains(key) && (BuiltIn.Contains(key) || IsProperty(key, out _)))
                    result.Add(key);
            }

            if (!result.Contains(Message))
                result.Add(Message);

            return result;
        }

        /// <summary>
        /// The layout with the column shown or hidden; a column that is shown is placed after the last column that comes before it by default
        /// (the built-in ones in their order, the properties after the logger, before the message)
        /// </summary>
        public static List<string> With(IEnumerable<string> keys, string key, bool visible)
        {
            var result = Normalize(keys);
            if (!visible)
            {
                if (key != Message)
                    result.Remove(key);

                return result;
            }

            if (result.Contains(key))
                return result;

            var rank = Rank(key);
            var after = result.FindLastIndex(other => Rank(other) <= rank);
            result.Insert(after + 1, key);
            return result;
        }

        private static double Rank(string key)
        {
            var index = BuiltIn.ToList().IndexOf(key);
            return index >= 0 ? index : BuiltIn.ToList().IndexOf(Thread) + 0.5;
        }

        /// <summary>
        /// The text of a property of an entry, or null when it has none
        /// </summary>
        public static string? ValueOf(LogEntry entry, string propertyName) =>
            entry.Properties.TryGetValue(propertyName, out var value) ? Convert.ToString(value, CultureInfo.CurrentCulture) : null;
    }
}
