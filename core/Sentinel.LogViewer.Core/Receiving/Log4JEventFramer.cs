using System.Collections.Generic;
using System.Text;

namespace Sentinel.LogViewer.Core;

/// <summary>
/// Cuts the text of a TCP connection into the events it holds. An event can arrive in several pieces, and several events can arrive in one;
/// whatever stands between the events (a line break, a separator) is dropped.
/// </summary>
public sealed class Log4JEventFramer
{
	private const string Start = "<log4j:event";
	private const string End = "</log4j:event>";

	/// <summary>An event that is longer than this is not an event; the text is dropped so that a client cannot fill the memory</summary>
	private const int MaxEventLength = 4 * 1024 * 1024;

	private readonly StringBuilder _pending = new();

	/// <summary>
	/// Adds the text that arrived and returns the events that are complete now
	/// </summary>
	public IEnumerable<string> Add(string text)
	{
		_pending.Append(text);
		var all = _pending.ToString();
		var position = 0;
		var events = new List<string>();
		while (true)
		{
			var start = all.IndexOf(Start, position, System.StringComparison.Ordinal);
			if (start < 0)
			{
				position = System.Math.Max(position, all.Length - (Start.Length - 1));
				break;
			}

			var end = all.IndexOf(End, start, System.StringComparison.Ordinal);
			if (end < 0)
			{
				position = start;
				break;
			}

			end += End.Length;
			events.Add(all.Substring(start, end - start));
			position = end;
		}

		_pending.Clear();
		if (all.Length - position <= MaxEventLength)
			_pending.Append(all, position, all.Length - position);

		return events;
	}
}
