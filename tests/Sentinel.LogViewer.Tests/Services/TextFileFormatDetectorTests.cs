using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Services;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// Unit tests for <see cref="TextFileFormatDetector"/>
/// </summary>
public class TextFileFormatDetectorTests : IDisposable
{
	private readonly string _file = Path.Combine(Path.GetTempPath(), "nlv_detect_" + Guid.NewGuid().ToString("N") + ".log");

	public void Dispose()
	{
		File.Delete(_file);
	}

	private TextFileFormat? Detect(IEnumerable<string> lines)
	{
		File.WriteAllLines(_file, lines);
		return new TextFileFormatDetector().DetectFormat(_file);
	}

	[Fact]
	public void DetectFormat_MostSampledLinesAreContinuationLines_StillDetectsColumns()
	{
		var entry = new[]
		{
			"2026-01-01 07:54:10.110\tDEBUG\tMy.App.OptionsSetupService\t\"{",
			"  \"\"Enabled\"\": true,",
			"  \"\"ClientId\"\": \"\"abc\"\"",
			"}\""
		};

		var format = Detect(Enumerable.Repeat(entry, 10).SelectMany(lines => lines));

		Assert.NotNull(format);
		Assert.Equal("\t", format.Separator);
		Assert.True(format.QuotedFields);
		Assert.Equal(4, format.ColumnCount);
		Assert.Equal(0, format.ColumnMapping.TimestampColumn);
		Assert.Equal(1, format.ColumnMapping.LevelColumn);
		Assert.Equal(2, format.ColumnMapping.LoggerColumn);
		Assert.Equal(3, format.ColumnMapping.MessageColumn);
	}

	[Fact]
	public void DetectFormat_PipeSeparatedFile_DoesNotUseQuotedFields()
	{
		var format = Detect(Enumerable.Range(1, 10).Select(i =>
			$"2025-01-01 10:00:00.000 | INFO | My.Logger.Name | \"quoted\" message {i}"));

		Assert.NotNull(format);
		Assert.Equal(" | ", format.Separator);
		Assert.False(format.QuotedFields);
	}

	[Fact]
	public void DetectFormat_GermanTimestamp_MapsItAsTimestampNotLogger()
	{
		var format = Detect(Enumerable.Range(1, 10).Select(i =>
			$"27.11.2025 14:53:05;INFO;My.Logger.Name;Message text number {i}"));

		Assert.NotNull(format);
		Assert.Equal(0, format.ColumnMapping.TimestampColumn);
		Assert.Equal(2, format.ColumnMapping.LoggerColumn);
	}
}
