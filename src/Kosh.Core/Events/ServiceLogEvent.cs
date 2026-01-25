using Kosh.Core.ValueObjects;

namespace Kosh.Core.Events;

public enum LogType
{
    Info,
    Error
}

public sealed record ServiceLogEvent(
    ServiceId ServiceId,
    string ServiceName,
    LogType Type,
    string Line
);
