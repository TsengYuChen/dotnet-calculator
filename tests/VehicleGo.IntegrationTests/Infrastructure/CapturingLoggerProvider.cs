using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace VehicleGo.IntegrationTests.Infrastructure;

/// <summary>One captured log entry with its structured fields.</summary>
public sealed record LogEntry(string Category, LogLevel Level, EventId EventId, string Message, IReadOnlyDictionary<string, object?> Fields);

/// <summary>Captures every log entry the app writes (all categories) so tests can inspect events and fields.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

    public IReadOnlyList<LogEntry> Event(string eventName) => Entries.Where(e => e.EventId.Name == eventName).ToList();

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var fields = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.First().Value)
                : new Dictionary<string, object?>();
            entries.Enqueue(new LogEntry(category, logLevel, eventId, formatter(state, exception), fields));
        }
    }
}
