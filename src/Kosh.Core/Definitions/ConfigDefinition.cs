using System.Runtime.InteropServices;
using Kosh.Core.ValueObjects;

namespace Kosh.Core.Definitions;

public sealed record ConfigDefinition(
    string ProjectName,
    OSPlatform OsPlatform,
    IReadOnlyList<GroupDefinition> Groups);