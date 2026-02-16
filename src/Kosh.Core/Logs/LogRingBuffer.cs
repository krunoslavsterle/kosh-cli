namespace Kosh.Core.Logs;

public class LogRingBuffer
{
    private readonly LogEntry[] _buffer;
    private int _nextIndex = 0;
    private int _count = 0;

    public LogRingBuffer(int capacity = 20000)
    {
        _buffer = new LogEntry[capacity];
    }

    public void Add(LogEntry entry)
    {
        _buffer[_nextIndex] = entry;
        _nextIndex = (_nextIndex + 1) % _buffer.Length;
        _count = Math.Min(_count + 1, _buffer.Length);
    }

    public IReadOnlyList<LogEntry> GetRange(int offset, int limit)
    {
        var result = new List<LogEntry>(limit);

        for (var i = offset; i < offset + limit && i < _count; i++)
        {
            var index = (_nextIndex - 1 - i + _buffer.Length) % _buffer.Length;
            result.Add(_buffer[index]);
        }

        return result;
    }
}
