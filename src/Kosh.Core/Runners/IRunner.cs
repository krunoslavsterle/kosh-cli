using FluentResults;
using Kosh.Core.Definitions;

namespace Kosh.Core.Runners;

public interface IRunner
{
    Task<Result<IRunningProcess>> StartAsync(ServiceDefinition service, CancellationToken ct);
}