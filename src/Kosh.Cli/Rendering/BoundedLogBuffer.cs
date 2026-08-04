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

    public List<LogEntry> GetLogs(string? serviceFilter = null)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(serviceFilter) || serviceFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
                return _allLogs.ToList();

            return _serviceLogs.TryGetValue(serviceFilter, out var list) ? list.ToList() : new List<LogEntry>();
        }
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
