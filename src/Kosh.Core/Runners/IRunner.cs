using Kosh.Core.Definitions;

namespace Kosh.Core.Runners;

public interface IRunner
{
    Task<IRunningProcess> StartAsync(ServiceDefinition service, CancellationToken ct);
}
