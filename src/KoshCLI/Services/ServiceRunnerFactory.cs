using FluentResults;
using KoshCLI.Config;
using KoshCLI.Services.Docker;
using KoshCLI.Services.Dotnet;
using KoshCLI.Services.Node;
using KoshCLI.Services.Proxy;

namespace KoshCLI.Services;

internal static class ServiceRunnerFactory
{
    public static Result<IServiceRunner> Create(ServiceConfig config, string rootDirectory)
    {
        if (config.Type == ServiceRunnerType.DotnetWatch)
            return new DotnetWatchServiceRunner(config, rootDirectory);

        if (config.Type == ServiceRunnerType.DotnetRun)
            return new DotnetRunServiceRunner(config, rootDirectory);

        if (config.Type == ServiceRunnerType.DockerCompose)
            return new DockerComposeServiceRunner(config, rootDirectory);

        if (config.Type == ServiceRunnerType.Caddy)
            return new CaddyServiceRunner(config, rootDirectory);

        if (config.Type == ServiceRunnerType.Node)
            return new NodeServiceRunner(config, rootDirectory);

        return Result.Fail($"Service type [{config.Type}] is not supported.");
    }
}

internal static class ServiceRunnerType
{
    public const string DotnetRun = "dotnet-run";
    public const string DotnetWatch = "dotnet-watch";
    public const string DockerCompose = "docker-compose";
    public const string Caddy = "caddy";
    public const string Node = "node";
}
