using Kosh.Core.Definitions;
using Kosh.Core.Runners;

namespace Kosh.Core.Runtime;

public sealed class ServiceRuntime
{
    public ServiceDefinition Definition { get; }
    public ServiceStatus Status { get; set; }
    public IRunningProcess? Process { get; internal set; }
    public TaskCompletionSource<int> Completion { get; } = new();

    public ServiceRuntime(ServiceDefinition definition)
    {
        Definition = definition;
        Status = ServiceStatus.NotStarted;
    }

    public void SetProcess(IRunningProcess process)
    {
        Process = process;
    }
}
