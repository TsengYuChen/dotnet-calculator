using Microsoft.Extensions.Logging;

namespace VehicleGo.UnitTests.Fakes;

/// <summary>One captured log entry with its structured fields.</summary>
public sealed record LogEntry(LogLevel Level, EventId EventId, string Message, IReadOnlyDictionary<string, object?> Fields);

/// <summary>Captures log entries; also writes <c>LOG:&lt;event name&gt;</c> to the shared <see cref="CallLog"/>.</summary>
public sealed class CapturingLogger<T>(CallLog? log = null) : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var fields = state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? pairs.ToDictionary(p => p.Key, p => p.Value)
            : new Dictionary<string, object?>();
        Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception), fields));
        log?.Add($"LOG:{eventId.Name}");
    }
}
