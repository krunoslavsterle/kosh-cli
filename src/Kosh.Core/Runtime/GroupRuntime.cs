using Kosh.Core.Definitions;

namespace Kosh.Core.Runtime;

public sealed class GroupRuntime
{
    public GroupDefinition Definition { get; }
    public GroupStatus Status { get; set; }
    public IReadOnlyList<ServiceRuntime> Services { get; }

    public GroupRuntime(GroupDefinition definition, IReadOnlyList<ServiceRuntime> services)
    {
        Definition = definition;
        Services = services;
        Status = GroupStatus.NotStarted;
    }
}
