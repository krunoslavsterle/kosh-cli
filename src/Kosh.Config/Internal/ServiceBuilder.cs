using FluentResults;
using Kosh.Config.Models;
using Kosh.Core.Definitions;
using Kosh.Core.Runners;
using Kosh.Core.ValueObjects;

namespace Kosh.Config.Internal;

internal class ServiceBuilder
{
    public static Result<ServiceDefinition> Create(YamlService yamlService, string rootDirectory)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(rootDirectory, yamlService.Path!));
        return CreateAbsolute(yamlService, absolutePath);
    }
    
    public static Result<ServiceDefinition> CreateAbsolute(YamlService yamlService, string absolutePath)
    {
        var runnerTypeResult = ParseRunnerType(yamlService.Type!);
        if (runnerTypeResult.IsFailed)
            return runnerTypeResult.ToResult();
        
        return new ServiceDefinition(
            Id: new ServiceId(Guid.NewGuid().ToString()),
            Name: yamlService.Name!,
            RunnerType: runnerTypeResult.Value,
            WorkingDirectory: absolutePath,
            Args: yamlService.Args,
            Environment: yamlService.Env,
            InheritEnv: yamlService.InheritEnv
        );
    }

    private static Result<RunnerType> ParseRunnerType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "dotnet-run" => RunnerType.DotnetRun,
            "dotnet-watch" => RunnerType.DotnetWatch,
            "dotnet-watch-alt" => RunnerType.DotnetWatchAlt,
            "docker-compose" => RunnerType.DockerCompose,
            "node" => RunnerType.Node,
            "caddy" => RunnerType.Caddy,
            _ => Result.Fail<RunnerType>($"Service type {type} is not recognized.")
        };
    }
}