using FluentResults;
using Kosh.Core.Runners;
using Kosh.Runners.Runner.Docker;
using Kosh.Runners.Runner.Dotnet;
using Kosh.Runners.Runner.Node;
using Kosh.Runners.Runner.Proxy;

namespace Kosh.Runners;

public sealed class RunnerFactory : IRunnerFactory
{
    public Result<IRunner> Create(RunnerType type)
    {
        return type switch
        {
            RunnerType.DotnetRun => new DotnetRunRunner(),
            RunnerType.DotnetWatch => new DotnetWatchRunner(),
            RunnerType.DockerCompose => new DockerComposeRunner(),
            RunnerType.Npm => new NpmRunner(),
            RunnerType.Caddy => new CaddyRunner(),
            _ => Result.Fail($"Runner type [{type}] is not yet supported")
        };
    }
}
