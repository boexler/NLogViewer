using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Sentinel.NLogViewer.App.Models;

namespace Sentinel.NLogViewer.App.Parsers;

/// <summary>
/// Parser for Log4J XML event format.
/// Parses XML strings conforming to the log4j.dtd specification into Log4JEvent objects.
/// </summary>
public class Log4JEventParser
{
	private const string MissingNs = "xmlns:log4j=\"http://jakarta.apache.org/log4j/\"";

	private static string EnsureNamespace(string xml)
	{
		if (xml.Contains("xmlns:log4j"))
			return xml;

		// insert namespace into root element
		return xml.Replace("<log4j:event", $"<log4j:event {MissingNs}");
	}

	/// <summary>
	/// Parses a Log4J XML event string into a Log4JEvent object.
	/// </summary>
	/// <param name="xml">The XML string containing a log4j:event element.</param>
	/// <returns>A Log4JEvent object representing the parsed event.</returns>
	/// <exception cref="ArgumentException">Thrown when the XML input is empty or null.</exception>
	/// <exception cref="FormatException">Thrown when the XML format is invalid or does not conform to the expected structure.</exception>
	public Log4JEvent Parse(string xml)
	{
		if (string.IsNullOrWhiteSpace(xml))
			throw new ArgumentException("XML input is empty.");

		XDocument doc;
		try
		{
			doc = XDocument.Parse(EnsureNamespace(xml), LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
		}
		catch (Exception ex)
		{
			throw new FormatException("Invalid XML format.", ex);
		}

		var ns = doc.Root?.Name.Namespace ?? "";

		var root = doc.Root;
		if (root == null || root.Name.LocalName != "event")
			throw new FormatException("Root element must be <log4j:event>.");

		var evt = new Log4JEvent
		{
			Logger = (string?)root.Attribute("logger") ?? string.Empty,
			Level = ParseLogLevel((string?)root.Attribute("level")),
			Thread = (string?)root.Attribute("thread") ?? string.Empty,
			Timestamp = ParseLong((string?)root.Attribute("timestamp")),
			Message = ExtractMessage(root, ns),
			Throwable = ExtractValue(root.Element(ns + "throwable")),
			LocationInfo = ExtractLocationInfo(root, ns),
			Properties = ExtractProperties(root, ns)
		};

		if (string.IsNullOrEmpty(evt.Thread))
			evt.Thread = ExtractValue(root.Element(ns + "thread")) ?? string.Empty;

		return evt;
	}

	/// <summary>
	/// Extracts the message content from the log4j:message child element.
	/// Handles both CDATA sections and normal text content correctly.
	/// </summary>
	/// <param name="root">The root log4j:event element.</param>
	/// <param name="ns">The XML namespace for log4j elements.</param>
	/// <returns>The trimmed message content, or an empty string if the message element is not found.</returns>
	private string ExtractMessage (XElement root, XNamespace ns)
	{
		var msgElement = root.Element(ns + "message");
		if (msgElement == null)
			return string.Empty;

		// Handles CDATA and normal text correctly
		return msgElement.Value?.Trim() ?? string.Empty;
	}

	/// <summary>
	/// Extracts the value content from an XML element.
	/// Handles both CDATA sections and normal text content correctly.
	/// </summary>
	/// <param name="element">The XML element to extract the value from. Can be null.</param>
	/// <returns>The trimmed element value, or null if the element is null or empty.</returns>
	private string? ExtractValue(XElement? element)
	{
		if (element == null)
			return null;

		// Handles CDATA and normal text correctly
		var value = element.Value?.Trim();
		return string.IsNullOrEmpty(value) ? null : value;
	}

	/// <summary>
	/// Extracts location information from the log4j:locationInfo child element.
	/// </summary>
	/// <param name="root">The root log4j:event element.</param>
	/// <param name="ns">The XML namespace for log4j elements.</param>
	/// <returns>A Log4JLocationInfo object, or null if the locationInfo element is not present.</returns>
	private Log4JLocationInfo? ExtractLocationInfo(XElement root, XNamespace ns)
	{
		var e = root.Element(ns + "locationInfo");
		if (e == null)
			return null;

		return new Log4JLocationInfo
		{
			Class = (string?)e.Attribute("class") ?? string.Empty,
			Method = (string?)e.Attribute("method") ?? string.Empty,
			File = (string?)e.Attribute("file") ?? string.Empty,
			Line = ParseNullableInt((string?)e.Attribute("line"))
		};
	}

	/// <summary>
	/// Extracts properties from the log4j:properties/log4j:data child elements.
	/// </summary>
	/// <param name="root">The root log4j:event element.</param>
	/// <param name="ns">The XML namespace for log4j elements.</param>
	/// <returns>A dictionary of property name-value pairs. Returns an empty dictionary if no properties element is found.</returns>
	private Dictionary<string, string> ExtractProperties(XElement root, XNamespace ns)
	{
		var dict = new Dictionary<string, string>();

		var props = root.Element(ns + "properties");
		if (props == null)
			return dict;

		foreach (var d in props.Elements(ns + "data"))
		{
			var name = (string?)d.Attribute("name");
			var value = (string?)d.Attribute("value");

			if (!string.IsNullOrEmpty(name))
				dict[name] = value ?? string.Empty;
		}

		return dict;
	}

	/// <summary>
	/// Parses a string representation of a log level into a Log4JLevel enum value.
	/// Handles case-insensitive matching and common variations (e.g., "WARNING" maps to "Warn").
	/// </summary>
	/// <param name="levelString">The string representation of the log level (e.g., "INFO", "ERROR").</param>
	/// <returns>The corresponding Log4JLevel enum value, or Log4JLevel.Unknown if the string cannot be parsed.</returns>
	private Log4JLevel ParseLogLevel(string? levelString)
	{
		if (string.IsNullOrWhiteSpace(levelString))
			return Log4JLevel.Unknown;

		return levelString.ToUpperInvariant() switch
		{
			"ALL" => Log4JLevel.All,
			"TRACE" => Log4JLevel.Trace,
			"DEBUG" => Log4JLevel.Debug,
			"INFO" => Log4JLevel.Info,
			"WARN" or "WARNING" => Log4JLevel.Warn,
			"ERROR" => Log4JLevel.Error,
			"FATAL" => Log4JLevel.Fatal,
			"OFF" => Log4JLevel.Off,
			_ => Log4JLevel.Unknown
		};
	}

	/// <summary>
	/// Parses a string representation of a long integer value.
	/// </summary>
	/// <param name="value">The string to parse.</param>
	/// <returns>The parsed long value, or 0 if parsing fails.</returns>
	private long ParseLong(string? value)
	{
		if (string.IsNullOrEmpty(value))
			return 0L;
		return long.TryParse(value, out long result) ? result : 0L;
	}

	/// <summary>
	/// Parses a string representation of an integer value into a nullable int.
	/// </summary>
	/// <param name="value">The string to parse.</param>
	/// <returns>The parsed integer value, or null if parsing fails or the input is null/empty.</returns>
	private int? ParseNullableInt(string? value)
	{
		if (string.IsNullOrEmpty(value))
			return null;
		return int.TryParse(value, out int result) ? result : null;
	}
}