using System.Diagnostics;
using FluentResults;
using KoshCLI.Config;

namespace KoshCLI.Services.Dotnet;

internal sealed class DotnetWatchServiceRunner : DotnetServiceRunnerBase, IServiceRunner
{
    private DotnetProjectConfiguration? _projectConfiguration;
    private Process? _process;
    
    private readonly string _rootDirectory;
    
    public DotnetWatchServiceRunner(ServiceConfig serviceConfig, string rootDirectory)
        : base(serviceConfig)
    {
        _rootDirectory = rootDirectory;
    }
    
    public Result Setup()
    {
        var workingDirectory = Path.GetFullPath(Path.Combine(_rootDirectory, ServiceConfig.Path!));
        var configurationResult = CreateProjectConfiguration(workingDirectory);
        if (configurationResult.IsFailed)
            return configurationResult.ToResult();

        _projectConfiguration = configurationResult.Value;

        return Result.Ok();
    }

    public void Start(CancellationToken ct)
    {
        _process = Watch(_projectConfiguration!);
    }

    public void Dispose()
    {
        Stop(_process!);
        _projectConfiguration = null;
        _process = null;
    }
}
