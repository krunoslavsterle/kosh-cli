namespace Kosh.Core.Events;

public sealed class ProcessLog
{
    public LogType Type { get; }
    public string Line { get; }

    public ProcessLog(LogType level, string line)
    {
        Type = level;
        Line = line;
    }
}