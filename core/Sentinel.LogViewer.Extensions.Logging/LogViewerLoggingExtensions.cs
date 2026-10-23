using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Extensions.Logging;

public static class LogViewerLoggingExtensions
{
	/// <summary>
	/// Sends what the application logs through <see cref="ILogger"/> to the log viewer. The entries go into
	/// <see cref="LogBuffer.Default"/>, which a viewer without any setting shows; the buffer is also registered as <see cref="LogBuffer"/> and
	/// <see cref="ILogSource"/>. This works without dependency injection too: <c>LoggerFactory.Create(builder =&gt; builder.AddLogViewer())</c>.
	/// </summary>
	/// <remarks>
	/// The minimum level of Microsoft.Extensions.Logging is Information unless the application sets another one (<c>SetMinimumLevel</c>).
	/// Scopes are taken as long as the logging captures them (<c>LoggerFactoryOptions.CaptureScopes</c>).
	/// </remarks>
	public static ILoggingBuilder AddLogViewer(this ILoggingBuilder builder, Action<LogViewerLoggerOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var options = new LogViewerLoggerOptions();
		configure?.Invoke(options);
		if (options.Capacity > 0)
			LogBuffer.Default = new LogBuffer(options.Capacity);

		var buffer = LogBuffer.Default;
		builder.Services.AddSingleton(buffer);
		builder.Services.AddSingleton<ILogSource>(buffer);
		builder.Services.AddSingleton<ILoggerProvider>(new LogViewerLoggerProvider(buffer));
		return builder;
	}

	/// <summary>
	/// <see cref="AddLogViewer(ILoggingBuilder, Action{LogViewerLoggerOptions}?)"/> for an application with dependency injection
	/// </summary>
	public static IServiceCollection AddLogViewer(this IServiceCollection services, Action<LogViewerLoggerOptions>? configure = null) =>
		services.AddLogging(builder => builder.AddLogViewer(configure));
}
