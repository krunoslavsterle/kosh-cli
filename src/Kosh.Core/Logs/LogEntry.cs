using Microsoft.Extensions.Logging;

namespace Kosh.Core.Logs;

public sealed class LogEntry
{
    public required DateTimeOffset Timestamp { get; init; }

    public required string Service { get; init; }

    public string? Group { get; init; }

    public required LogLevel Level { get; init; }

    public required string Message { get; init; }

    // Opcionalno: dodatni metapodaci (PID, port, thread, category...)
    public Dictionary<string, string>? Metadata { get; init; }
}
