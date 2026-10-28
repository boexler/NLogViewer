namespace Sentinel.LogViewer.Core;

/// <summary>
/// Which time an entry shows. The <see cref="LogEntry.TimeStamp"/> of an entry is a moment when its kind is <see cref="DateTimeKind.Utc"/> or
/// <see cref="DateTimeKind.Local"/> (the zone was known when the entry was read), and a clock time without a zone when its kind is
/// <see cref="DateTimeKind.Unspecified"/> (a text file that gives no zone); that one is read in the zone of the file. Nothing here changes an
/// entry: the conversion only decides what is shown.
/// </summary>
public static class EntryTime
{
	/// <summary>
	/// The time of an entry as it is shown in the display zone
	/// </summary>
	/// <param name="timeStamp">The <see cref="LogEntry.TimeStamp"/></param>
	/// <param name="fileZone">The zone in which a time without a zone was written; null for the local time of the computer</param>
	/// <param name="displayZone">The zone to show the time in; null for the local time of the computer</param>
	/// <returns>The clock time in the display zone; its kind is <see cref="DateTimeKind.Unspecified"/></returns>
	public static DateTime ToDisplay(DateTime timeStamp, TimeZoneInfo? fileZone, TimeZoneInfo? displayZone)
	{
		var display = displayZone ?? TimeZoneInfo.Local;
		var source = ZoneOf(timeStamp, fileZone);
		if (timeStamp.Kind == DateTimeKind.Unspecified && source.Equals(display))
			return timeStamp;

		// A clock time that does not exist (the hour that summer time skips) has no exception here: GetUtcOffset takes the standard offset.
		var clock = DateTime.SpecifyKind(timeStamp, DateTimeKind.Unspecified);
		var moment = new DateTimeOffset(clock, source.GetUtcOffset(clock));
		return TimeZoneInfo.ConvertTime(moment, display).DateTime;
	}

	/// <summary>
	/// A time stamp as a moment in UTC, to compare it with a time range: a time without a zone is read in the zone of the file
	/// </summary>
	public static DateTime ToUtc(DateTime timeStamp, TimeZoneInfo? fileZone)
	{
		var moment = ToMoment(timeStamp, fileZone);
		return moment.Kind == DateTimeKind.Local ? moment.ToUniversalTime() : moment;
	}

	/// <summary>
	/// Whether the time stamp of an entry lies in the time range; an open end is null, and both ends are inclusive
	/// </summary>
	public static bool IsInRange(DateTime timeStamp, TimeZoneInfo? fileZone, DateTime? from, DateTime? to)
	{
		if (from == null && to == null)
			return true;

		var moment = ToUtc(timeStamp, fileZone);
		return (from == null || moment >= from) && (to == null || moment <= to);
	}

	/// <summary>
	/// The id that stands for the local time of the computer where a zone is chosen from a list or saved
	/// </summary>
	public const string LocalId = "local";

	/// <summary>
	/// A time stamp as a moment: a time without a zone is read in the zone of the file and becomes UTC; the others stay as they are
	/// </summary>
	public static DateTime ToMoment(DateTime timeStamp, TimeZoneInfo? fileZone)
	{
		if (timeStamp.Kind != DateTimeKind.Unspecified)
			return timeStamp;

		var zone = fileZone ?? TimeZoneInfo.Local;
		return new DateTimeOffset(timeStamp, zone.GetUtcOffset(timeStamp)).UtcDateTime;
	}

	/// <summary>
	/// The zone that the clock time of a time stamp is in: the one of the kind, or the zone of the file for a time without a zone
	/// </summary>
	public static TimeZoneInfo ZoneOf(DateTime timeStamp, TimeZoneInfo? fileZone) => timeStamp.Kind switch
	{
		DateTimeKind.Utc => TimeZoneInfo.Utc,
		DateTimeKind.Local => TimeZoneInfo.Local,
		_ => fileZone ?? TimeZoneInfo.Local
	};

	/// <summary>The label of the local zone; the control shows its own word for it</summary>
	public const string LocalLabel = "local";

	/// <summary>
	/// A short name of a zone for headers and tooltips: <c>local</c>, <c>UTC</c>, or the offset like <c>UTC+9</c> or <c>UTC-3:30</c>
	/// </summary>
	public static string Label(TimeZoneInfo? zone)
	{
		if (zone == null)
			return LocalLabel;

		// UTC first: on a computer that runs in UTC, the zone that is named UTC is the local one as well, and it should still say UTC.
		if (zone.Equals(TimeZoneInfo.Utc))
			return "UTC";

		if (zone.Equals(TimeZoneInfo.Local))
			return LocalLabel;

		var offset = zone.BaseUtcOffset;
		var sign = offset < TimeSpan.Zero ? "-" : "+";
		offset = offset.Duration();
		return offset.Minutes == 0 ? $"UTC{sign}{offset.Hours}" : $"UTC{sign}{offset.Hours}:{offset.Minutes:00}";
	}

	/// <summary>
	/// The zone with an id of the computer, or null (local time) for an empty or unknown id
	/// </summary>
	public static TimeZoneInfo? FindZone(string? id)
	{
		if (string.IsNullOrWhiteSpace(id) || string.Equals(id, LocalId, StringComparison.OrdinalIgnoreCase))
			return null;

		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(id);
		}
		catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
		{
			return null;
		}
	}
}
