using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Sentinel.LogViewer;
using Sentinel.LogViewer.Models;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// Service for loading and saving text file format configurations per file pattern
/// </summary>
public class TextFileFormatConfigService
{
	private readonly string _configPath;

	public TextFileFormatConfigService()
	{
		var appDataPath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			ProductNames.Technical);
		Directory.CreateDirectory(appDataPath);
		_configPath = Path.Combine(appDataPath, "textfileformats.json");
	}

	/// <summary>
	/// Gets the format configuration for a specific file
	/// </summary>
	/// <param name="filePath">Path to the file</param>
	/// <returns>Format configuration if found, null otherwise</returns>
	public TextFileFormat? GetFormatForFile(string filePath)
	{
		var fileName = Path.GetFileName(filePath);
		var collection = LoadConfigurations();

		// Try exact filename match first
		var exactMatch = collection.Formats.FirstOrDefault(f => f.FilePattern == fileName);
		if (exactMatch != null)
			return exactMatch.Format;

		// Try pattern matching (simple wildcard support)
		foreach (var config in collection.Formats)
		{
			if (MatchesPattern(fileName, config.FilePattern))
			{
				return config.Format;
			}
		}

		return null;
	}

	/// <summary>
	/// Saves a format configuration for a file pattern
	/// </summary>
	/// <param name="filePattern">File pattern (filename or pattern)</param>
	/// <param name="format">Format configuration to save</param>
	public void SaveFormatForPattern(string filePattern, TextFileFormat format)
	{
		var collection = LoadConfigurations();

		// Remove existing entry for this pattern
		collection.Formats.RemoveAll(f => f.FilePattern == filePattern);

		// Add new entry
		collection.Formats.Add(new TextFileFormatConfig
		{
			FilePattern = filePattern,
			Format = format
		});

		SaveConfigurations(collection);
	}

	/// <summary>
	/// Removes a format configuration for a file pattern
	/// </summary>
	/// <param name="filePattern">File pattern to remove</param>
	public void RemoveFormatForPattern(string filePattern)
	{
		var collection = LoadConfigurations();
		collection.Formats.RemoveAll(f => f.FilePattern == filePattern);
		SaveConfigurations(collection);
	}

	/// <summary>
	/// Loads all format configurations from disk
	/// </summary>
	private TextFileFormatConfigCollection LoadConfigurations()
	{
		try
		{
			if (File.Exists(_configPath))
			{
				var json = File.ReadAllText(_configPath);
				var collection = JsonSerializer.Deserialize<TextFileFormatConfigCollection>(json);
				if (collection != null)
				{
					return collection;
				}
			}
		}
		catch (Exception)
		{
			// Fall back to empty collection
		}

		return new TextFileFormatConfigCollection();
	}

	/// <summary>
	/// Saves all format configurations to disk
	/// </summary>
	private void SaveConfigurations(TextFileFormatConfigCollection collection)
	{
		try
		{
			var options = new JsonSerializerOptions
			{
				WriteIndented = true,
				DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
			};
			var json = JsonSerializer.Serialize(collection, options);
			File.WriteAllText(_configPath, json);
		}
		catch (Exception)
		{
			// Log error but don't throw
		}
	}

	/// <summary>
	/// Checks if a filename matches a pattern (simple wildcard support: * and ?)
	/// </summary>
	private bool MatchesPattern(string fileName, string pattern)
	{
		if (string.IsNullOrEmpty(pattern))
			return false;

		// Simple wildcard matching
		if (pattern.Contains('*') || pattern.Contains('?'))
		{
			var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
				.Replace("\\*", ".*")
				.Replace("\\?", ".") + "$";

			return System.Text.RegularExpressions.Regex.IsMatch(fileName, regexPattern);
		}

		return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
	}
}

