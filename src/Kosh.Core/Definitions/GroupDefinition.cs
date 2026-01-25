using Kosh.Core.ValueObjects;

namespace Kosh.Core.Definitions;

public sealed record GroupDefinition(
    GroupId Id,
    string Name,
    ExecutionMode  ExecutionMode,
    IReadOnlyList<ServiceDefinition> Services
);
