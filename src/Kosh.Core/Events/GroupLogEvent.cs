using Kosh.Core.ValueObjects;

namespace Kosh.Core.Events;

public sealed record GroupLogEvent(
    GroupId GroupId,
    string GroupName,
    LogType Type,
    string Line
);