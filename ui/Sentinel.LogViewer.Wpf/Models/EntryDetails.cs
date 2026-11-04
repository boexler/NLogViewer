using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.Models
{
    /// <summary>
    /// What the Details tab of the panel shows about an entry: the texts in the language of the control, the exception, the properties and
    /// where in the source the entry was written
    /// </summary>
    public sealed class EntryDetails
    {
        // The names under which sources put the place of an entry: a Log4J message has a file name and a line number, a text file the number of the line.
        private const string FileName = "FileName";
        private const string LineNumber = "LineNumber";
        private const string FileLine = "FileLine";

        public EntryDetails(LogEntry entry, string time, string level)
        {
            Entry = entry;
            Time = time;
            Level = level;
            Properties = entry.Properties
                .Select(property => new KeyValuePair<string, string>(property.Key, Convert.ToString(property.Value, CultureInfo.InvariantCulture) ?? string.Empty))
                .OrderBy(property => property.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Source = SourceOf(entry);
        }

        public LogEntry Entry { get; }

        public string Time { get; }

        public string Level { get; }

        public string Logger => Entry.LoggerName;

        public string Message => Entry.Message;

        /// <summary>The exception with its stack trace; empty when the entry has none</summary>
        public string Exception => Entry.Exception ?? string.Empty;

        public bool HasException => Exception.Length > 0;

        /// <summary>The application and where it sent the entry from, e.g. "Acme · PC1 (10.0.0.5)"; empty when the source does not tell</summary>
        public string Origin => string.Join(" · ", new[] { Entry.Application, Entry.Sender }.Where(text => text.Length > 0));

        public bool HasOrigin => Origin.Length > 0;

        /// <summary>The file and the line, when the source tells them; empty otherwise</summary>
        public string Source { get; }

        public bool HasSource => Source.Length > 0;

        /// <summary>All properties of the entry as names and values, by name</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Properties { get; }

        /// <summary>The whole entry as text, for the clipboard</summary>
        public string FullText
        {
            get
            {
                var text = new StringBuilder();
                text.Append(Time).Append('\t').Append(Level).Append('\t').AppendLine(Logger);
                if (HasOrigin)
                    text.AppendLine(Origin);

                if (HasSource)
                    text.AppendLine(Source);

                text.AppendLine(Message);
                if (HasException)
                    text.AppendLine().AppendLine(Exception);

                foreach (var property in Properties)
                    text.Append(property.Key).Append(" = ").AppendLine(property.Value);

                return text.ToString().TrimEnd();
            }
        }

        private static string SourceOf(LogEntry entry)
        {
            var properties = entry.Properties;
            if (properties.TryGetValue(FileName, out var file) && Convert.ToString(file, CultureInfo.InvariantCulture) is { Length: > 0 } name)
                return properties.TryGetValue(LineNumber, out var line) && line != null ? $"{name}:{Convert.ToString(line, CultureInfo.InvariantCulture)}" : name;

            return properties.TryGetValue(FileLine, out var number) && number != null
                ? LogViewerTexts.Format("Det_Line", Convert.ToString(number, CultureInfo.InvariantCulture)!)
                : string.Empty;
        }
    }
}
