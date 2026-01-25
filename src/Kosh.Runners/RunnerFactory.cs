using FluentResults;
using Kosh.Core.Runners;
using Kosh.Runners.Runner.Dotnet;

namespace Kosh.Runners;

public sealed class RunnerFactory : IRunnerFactory
{
    public Result<IRunner> Create(RunnerType type)
    {
        return type switch
        {
            RunnerType.DotnetRun => new DotnetRunRunner(),
            // RunnerType.DotnetWatch => new DotnetWatchRunner(),
            // RunnerType.DockerCompose => new DockerComposeRunner(),
            // RunnerType.Node => new NodeRunner(),
            // RunnerType.Caddy => new CaddyRunner(),
            _ => Result.Fail($"Runner type [{type}] is not yet supported")
        };
    }
}
