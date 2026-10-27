using System;
using System.Collections;
using System.Globalization;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// Puts the entries in the order of a column. Equal values keep the order in which the entries came in, also when the sort is descending.
    /// </summary>
    internal sealed class EntryComparer : IComparer
    {
        private readonly string _key;
        private readonly bool _descending;
        private readonly TimeZoneInfo? _fileZone;

        /// <param name="key">The key of the column, see <see cref="LogColumns"/></param>
        /// <param name="fileZone">The zone in which times without a zone are read, so that the time stamps are compared as moments</param>
        public EntryComparer(string key, bool descending, TimeZoneInfo? fileZone)
        {
            _key = key;
            _descending = descending;
            _fileZone = fileZone;
        }

        public int Compare(object? x, object? y)
        {
            if (x is not LogEntry left || y is not LogEntry right)
                return 0;

            var result = CompareValues(left, right);
            if (_descending)
                result = -result;

            return result != 0 ? result : left.Id.CompareTo(right.Id);
        }

        private int CompareValues(LogEntry left, LogEntry right)
        {
            switch (_key)
            {
                case LogColumns.Id:
                    return left.Id.CompareTo(right.Id);
                case LogColumns.Level:
                    return left.Level.CompareTo(right.Level);
                case LogColumns.TimeStamp:
                    return EntryTime.ToMoment(left.TimeStamp, _fileZone).CompareTo(EntryTime.ToMoment(right.TimeStamp, _fileZone));
                case LogColumns.Logger:
                    return CompareText(left.LoggerName, right.LoggerName);
                case LogColumns.Message:
                    return CompareText(left.Message, right.Message);
                case LogColumns.Thread:
                    return CompareText(LogColumns.ValueOf(left, FilterTerm.ThreadProperty), LogColumns.ValueOf(right, FilterTerm.ThreadProperty));
                default:
                    return LogColumns.IsProperty(_key, out var name) ? CompareText(LogColumns.ValueOf(left, name), LogColumns.ValueOf(right, name)) : 0;
            }
        }

        /// <summary>
        /// Numbers come before text and are compared as numbers; text is compared without regard to case; a missing value comes first among the text.
        /// </summary>
        private static int CompareText(string? left, string? right)
        {
            left ??= string.Empty;
            right ??= string.Empty;

            var leftIsNumber = TryNumber(left, out var leftNumber);
            var rightIsNumber = TryNumber(right, out var rightNumber);
            if (leftIsNumber && rightIsNumber)
                return leftNumber.CompareTo(rightNumber);

            if (leftIsNumber != rightIsNumber)
                return leftIsNumber ? -1 : 1;

            return string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase);
        }

        // Most values are text; looking at the first character first saves the parsing, which would be done for every comparison of a sort.
        private static bool TryNumber(string text, out double number)
        {
            number = 0;
            return text.Length > 0 && (char.IsDigit(text[0]) || text[0] is '-' or '+' or '.' or ',')
                && double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out number);
        }
    }
}
