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
            RunnerDefinition: runnerTypeResult.Value,
            WorkingDirectory: absolutePath,
            Args: yamlService.Args,
            Environment: yamlService.Env,
            InheritEnv: yamlService.InheritEnv
        );
    }

    private static Result<RunnerTypeDefinition> ParseRunnerType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "dotnet-run" => new RunnerTypeDefinition(RunnerType.DotnetRun, ExecutionMode.BlockingUntilExit),
            "dotnet-watch" => new RunnerTypeDefinition(RunnerType.DotnetWatch, ExecutionMode.NonBlocking),
            "dotnet-watch-alt" => new RunnerTypeDefinition(RunnerType.DotnetWatchAlt, ExecutionMode.NonBlocking),
            "docker-compose" => new RunnerTypeDefinition(RunnerType.DockerCompose, ExecutionMode.BlockingUntilReady),
            "npm" => new RunnerTypeDefinition(RunnerType.Npm, ExecutionMode.NonBlocking),
            "caddy" => new RunnerTypeDefinition(RunnerType.Caddy, ExecutionMode.NonBlocking),
            _ => Result.Fail<RunnerTypeDefinition>($"Service type {type} is not recognized.")
        };
    }
}