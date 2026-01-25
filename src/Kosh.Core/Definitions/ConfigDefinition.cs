using Kosh.Core.ValueObjects;

namespace Kosh.Core.Definitions;

public sealed record ConfigDefinition(
    string ProjectName,
    IReadOnlyList<GroupDefinition> Groups);