using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Extensions.Logging;

/// <summary>
/// Turns what an application logs through <see cref="ILogger"/> into log entries and puts them into a <see cref="LogBuffer"/>
/// </summary>
public sealed class LogViewerLoggerProvider(LogBuffer buffer) : ILoggerProvider, ISupportExternalScope
{
	private const string OriginalFormat = "{OriginalFormat}";

	private IExternalScopeProvider? _scopes;

	public ILogger CreateLogger(string categoryName) => new LogViewerLogger(categoryName, buffer, () => _scopes);

	public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

	public void Dispose()
	{
	}

	private sealed class LogViewerLogger(string category, LogBuffer buffer, Func<IExternalScopeProvider?> scopes) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => scopes()?.Push(state);

		public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (!IsEnabled(logLevel))
				return;

			var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
			{
				[FilterTerm.ThreadProperty] = Thread.CurrentThread.Name ?? Environment.CurrentManagedThreadId.ToString()
			};
			if (eventId.Id != 0)
				properties["EventId"] = eventId.Id;

			if (eventId.Name != null)
				properties["EventName"] = eventId.Name;

			if (state is IEnumerable<KeyValuePair<string, object?>> values)
				Add(properties, values);

			scopes()?.ForEachScope(static (scope, bag) =>
			{
				if (scope is IEnumerable<KeyValuePair<string, object?>> scopeValues)
					Add(bag, scopeValues);
				else if (scope != null)
					bag["Scope"] = bag.TryGetValue("Scope", out var outer) ? $"{outer} => {scope}" : scope.ToString();
			}, properties);

			// The levels of Microsoft.Extensions.Logging have the numbers of ours: Trace to Critical are 0 to 5.
			var level = (LogEntryLevel)Math.Clamp((int)logLevel, 0, (int)LogEntryLevel.Fatal);
			buffer.Add(new LogEntry(level, category, formatter(state, exception))
			{
				Exception = exception?.ToString(),
				Properties = properties
			});
		}

		private static void Add(Dictionary<string, object?> properties, IEnumerable<KeyValuePair<string, object?>> values)
		{
			foreach (var (name, value) in values)
			{
				if (name != OriginalFormat)
					properties[name] = value;
			}
		}
	}
}
