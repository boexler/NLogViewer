using System;

namespace Sentinel.NLogViewer.App.Models;

public record AppName
{
	public AppName(string name, string id)
	{
		Name = name;
		Id = id;
	}

	public string Name { get; }
	public string Id { get; }

	public override string ToString() => $"{Name};{Id}";
}

/// <summary>
/// Represents application information extracted from log events
/// </summary>
public class AppInfo
{
	/// <summary>
	/// Application name. May be null when the source log event carries no usable app identifier.
	/// </summary>
	public AppName? AppName { get; set; } = new("Unknown", Guid.NewGuid().ToString());

	/// <summary>
	/// Sender/Remote endpoint information
	/// </summary>
	public string Sender { get; set; } = "Local";

	/// <summary>
	/// Unique identifier combining Name and Sender
	/// </summary>
	public string Id => $"{AppName};{Sender}";

	public override string ToString()
	{
		var name = AppName?.Name ?? string.Empty;
		return string.IsNullOrEmpty(Sender) ? name : $"{name} @ {Sender}";
	}

	public override bool Equals(object? obj)
	{
		if (obj is AppInfo other)
		{
			return Id.Equals(other.Id, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return Id.GetHashCode(StringComparison.OrdinalIgnoreCase);
	}
}