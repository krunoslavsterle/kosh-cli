using System.Collections.Concurrent;
using Kosh.Core.Events;

namespace Kosh.Cli.Rendering;

public sealed record LogEntry(
    string ServiceName,
    string Message,
    bool IsError,
    DateTime Timestamp
);

public sealed class BoundedLogBuffer
{
    private readonly object _lock = new();
    private readonly int _maxCapacity;
    private readonly LinkedList<LogEntry> _allLogs = new();
    private readonly Dictionary<string, LinkedList<LogEntry>> _serviceLogs = new(StringComparer.OrdinalIgnoreCase);

    public BoundedLogBuffer(int maxCapacity = 5000)
    {
        _maxCapacity = maxCapacity;
    }

    public void Add(LogEntry entry)
    {
        lock (_lock)
        {
            // Multiline Stack Trace Grouping:
            // If the line is a stack trace continuation line from the same service, merge with previous entry!
            if (_allLogs.Count > 0 && IsStackTraceContinuation(entry.Message))
            {
                var lastEntry = _allLogs.Last!.Value;
                if (lastEntry.ServiceName.Equals(entry.ServiceName, StringComparison.OrdinalIgnoreCase))
                {
                    var mergedEntry = lastEntry with { Message = lastEntry.Message + "\n" + entry.Message };
                    _allLogs.RemoveLast();
                    _allLogs.AddLast(mergedEntry);

                    if (_serviceLogs.TryGetValue(entry.ServiceName, out var sList) && sList.Count > 0)
                    {
                        sList.RemoveLast();
                        sList.AddLast(mergedEntry);
                    }
                    return;
                }
            }

            _allLogs.AddLast(entry);

            if (!_serviceLogs.TryGetValue(entry.ServiceName, out var list))
            {
                list = new LinkedList<LogEntry>();
                _serviceLogs[entry.ServiceName] = list;
            }
            list.AddLast(entry);

            if (_allLogs.Count > _maxCapacity)
            {
                var oldest = _allLogs.First!.Value;
                _allLogs.RemoveFirst();

                if (_serviceLogs.TryGetValue(oldest.ServiceName, out var sList))
                {
                    sList.RemoveFirst();
                    if (sList.Count == 0)
                        _serviceLogs.Remove(oldest.ServiceName);
                }
            }
        }
    }

    private static bool IsStackTraceContinuation(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var trimmed = message.TrimStart();
        return message.StartsWith('\t') ||
               message.StartsWith("   ") ||
               message.StartsWith("  ") ||
               trimmed.StartsWith("at ") ||
               trimmed.StartsWith("---> ") ||
               trimmed.StartsWith("Caused by:") ||
               trimmed.StartsWith("File \"") ||
               trimmed.StartsWith("Traceback ");
    }

    public List<LogEntry> GetLogs(string? serviceFilter = null)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(serviceFilter) || serviceFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
                return _allLogs.ToList();

            return _serviceLogs.TryGetValue(serviceFilter, out var list) ? list.ToList() : new List<LogEntry>();
        }
    }

    public List<LogEntry> SearchLogs(string? targetService, string query)
    {
        var baseLogs = GetLogs(targetService);
        if (string.IsNullOrWhiteSpace(query))
            return baseLogs;

        query = query.Trim();

        return baseLogs.Where(e => e.Message.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _allLogs.Clear();
            _serviceLogs.Clear();
        }
    }

    public IReadOnlyList<string> GetKnownServices()
    {
        lock (_lock)
        {
            return _serviceLogs.Keys.OrderBy(k => k).ToList();
        }
    }
}
