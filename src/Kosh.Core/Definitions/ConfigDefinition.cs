using System.Runtime.InteropServices;
using Kosh.Core.ValueObjects;

namespace Kosh.Core.Definitions;

public sealed record ConfigDefinition(
    string ProjectName,
    string? RootDirectory,
    OSPlatform OsPlatform,
    List<HostDefinition> Hosts,
    IReadOnlyList<GroupDefinition> ServiceGroups);