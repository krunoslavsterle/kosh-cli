using Kosh.Core.Runners;
using Kosh.Core.ValueObjects;

namespace Kosh.Core.Definitions;

public sealed record ServiceDefinition(
    ServiceId Id,
    string Name,
    RunnerTypeDefinition RunnerDefinition,
    string WorkingDirectory,
    string? Args,
    IReadOnlyDictionary<string, string> Environment,
    bool InheritEnv);