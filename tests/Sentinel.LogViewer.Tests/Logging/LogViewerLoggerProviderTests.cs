using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Extensions.Logging;
using Xunit;

namespace Sentinel.LogViewer.Tests.Logging;

/// <summary>
/// Tests for <c>AddLogViewer()</c>: what an <see cref="ILogger"/> writes becomes log entries, with and without dependency injection.
/// They share the default buffer with the tests of the viewer, so they run in the same collection.
/// </summary>
[Collection(WpfCollection.Name)]
public sealed class LogViewerLoggerProviderTests : IDisposable
{
	private readonly LogBuffer _defaultBefore = LogBuffer.Default;

	public void Dispose() => LogBuffer.Default = _defaultBefore;

	private static List<LogEntry> Entries(LogBuffer buffer)
	{
		var entries = new List<LogEntry>();
		using (buffer.Entries.Subscribe(entries.Add))
		{
		}

		return entries;
	}

	private static ILoggerFactory Factory(LogBuffer buffer, Action<ILoggingBuilder>? configure = null) =>
		LoggerFactory.Create(builder =>
		{
			builder.SetMinimumLevel(LogLevel.Trace);
			builder.Services.AddSingleton<ILoggerProvider>(new LogViewerLoggerProvider(buffer));
			configure?.Invoke(builder);
		});

	[Theory]
	[InlineData(LogLevel.Trace, LogEntryLevel.Trace)]
	[InlineData(LogLevel.Debug, LogEntryLevel.Debug)]
	[InlineData(LogLevel.Information, LogEntryLevel.Info)]
	[InlineData(LogLevel.Warning, LogEntryLevel.Warn)]
	[InlineData(LogLevel.Error, LogEntryLevel.Error)]
	[InlineData(LogLevel.Critical, LogEntryLevel.Fatal)]
	public void TheLevels_BecomeTheSameLevel_AndTheCategoryTheLogger(LogLevel level, LogEntryLevel expected)
	{
		var buffer = new LogBuffer();
		using var factory = Factory(buffer);

		factory.CreateLogger("Acme.Shop").Log(level, "{Count} orders in {Ms} ms", 3, 12);

		var entry = Assert.Single(Entries(buffer));
		Assert.Equal(expected, entry.Level);
		Assert.Equal("Acme.Shop", entry.LoggerName);
		Assert.Equal("3 orders in 12 ms", entry.Message);
		Assert.Null(entry.Exception);
	}

	[Fact]
	public void TheValuesOfAStructuredMessage_TheEventAndTheThread_AreProperties()
	{
		var buffer = new LogBuffer();
		using var factory = Factory(buffer);

		factory.CreateLogger("a").Log(LogLevel.Information, new EventId(42, "OrderCreated"), "{Count} orders in {Ms} ms", 3, 12);

		var properties = Assert.Single(Entries(buffer)).Properties;
		Assert.Equal(3, properties["Count"]);
		Assert.Equal(12, properties["Ms"]);
		Assert.Equal(42, properties["EventId"]);
		Assert.Equal("OrderCreated", properties["EventName"]);
		Assert.Equal(System.Threading.Thread.CurrentThread.Name ?? Environment.CurrentManagedThreadId.ToString(), properties[FilterTerm.ThreadProperty]);
		Assert.DoesNotContain("{OriginalFormat}", properties.Keys);
	}

	[Fact]
	public void TheException_BecomesItsText_AndTheMessageStaysTheMessage()
	{
		var buffer = new LogBuffer();
		using var factory = Factory(buffer);

		factory.CreateLogger("a").LogError(new InvalidOperationException("boom"), "failed");

		var entry = Assert.Single(Entries(buffer));
		Assert.Equal("failed", entry.Message);
		Assert.Contains("InvalidOperationException", entry.Exception);
		Assert.Contains("boom", entry.Exception);
	}

	[Fact]
	public void TheScopes_AddTheirValues_AndTheirText()
	{
		var buffer = new LogBuffer();
		using var factory = Factory(buffer);
		var logger = factory.CreateLogger("a");

		using (logger.BeginScope(new Dictionary<string, object?> { ["OrderId"] = 4711 }))
		using (logger.BeginScope("checkout"))
		{
			logger.LogInformation("paid");
		}

		var properties = Assert.Single(Entries(buffer)).Properties;
		Assert.Equal(4711, properties["OrderId"]);
		Assert.Equal("checkout", properties["Scope"]);
	}

	[Fact]
	public void WithoutDependencyInjection_AddLogViewer_WritesToTheDefaultBuffer()
	{
		LogBuffer.Default = new LogBuffer();

		using var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddLogViewer());
		factory.CreateLogger("a").LogDebug("hello");

		Assert.Equal("hello", Assert.Single(Entries(LogBuffer.Default)).Message);
	}

	[Fact]
	public void AnOtherCapacity_GivesTheLoggingItsOwnBuffer_WhichBecomesTheDefault()
	{
		var before = LogBuffer.Default;

		using var factory = LoggerFactory.Create(builder => builder.AddLogViewer(options => options.Capacity = 3));

		Assert.NotSame(before, LogBuffer.Default);
		Assert.Equal(3, LogBuffer.Default.Capacity);
	}

	[Fact]
	public void WithDependencyInjection_TheBufferIsRegistered_AsBufferAndAsSource_AndIsTheDefault()
	{
		var services = new ServiceCollection();
		services.AddLogViewer(options => options.Capacity = 5);
		using var provider = services.BuildServiceProvider();

		var buffer = provider.GetRequiredService<LogBuffer>();
		Assert.Same(buffer, provider.GetRequiredService<ILogSource>());
		Assert.Same(buffer, LogBuffer.Default);
		Assert.Equal(5, buffer.Capacity);

		provider.GetRequiredService<ILoggerFactory>().CreateLogger("a").LogWarning("through the container");
		Assert.Equal("through the container", Assert.Single(Entries(buffer)).Message);
	}

	[Fact]
	public void TheDefaultLevelOfMicrosoftExtensionsLogging_IsKept_SoDebugNeedsSetMinimumLevel()
	{
		LogBuffer.Default = new LogBuffer();

		using var factory = LoggerFactory.Create(builder => builder.AddLogViewer());
		var logger = factory.CreateLogger("a");
		logger.LogDebug("hidden");
		logger.LogInformation("shown");

		Assert.Equal(new[] { "shown" }, Entries(LogBuffer.Default).Select(entry => entry.Message));
	}
}
