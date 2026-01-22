using System.Diagnostics;
using FluentResults;
using KoshCLI.Config;
using KoshCLI.Terminal;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace KoshCLI.Services.Dotnet;

internal class DotnetRunServiceRunner : DotnetServiceRunnerBase, IServiceRunner
{
    private List<DotnetProjectConfiguration> _projectConfigurations = [];
    private List<Process> _processes = [];
    private Stopwatch sw = new Stopwatch();
    
    private readonly bool _isGlobPath = false;
    private readonly string _rootDirectory;

    public DotnetRunServiceRunner(ServiceConfig serviceConfig, string rootDirectory)
        : base(serviceConfig)
    {
        _rootDirectory = rootDirectory;
        _isGlobPath = serviceConfig.Path!.Contains('*') || serviceConfig.Path.Contains('?');
    }

    public Result Setup()
    {
        if (_isGlobPath)
        {
            var matcher = new Matcher();

            if (ServiceConfig.Path!.EndsWith(".csproj"))
                matcher.AddInclude(ServiceConfig.Path);
            else
                matcher.AddInclude($"{ServiceConfig.Path}/*.csproj");

            var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(_rootDirectory)));
            var projects = result.Files.Select(f => f.Path).ToList();

            foreach (var projectPath in projects)
            {
                var fullPath = Path.Combine(_rootDirectory, projectPath);
                var projectConfigurationResult = CreateProjectConfiguration(fullPath);
                if (projectConfigurationResult.IsFailed)
                    return projectConfigurationResult.ToResult();

                _projectConfigurations.Add(projectConfigurationResult.Value);
            }

            return Result.Ok();
        }

        
        var workingDirectory = Path.GetFullPath(Path.Combine(_rootDirectory, ServiceConfig.Path!));
        var configurationResult = CreateProjectConfiguration(workingDirectory);
        if (configurationResult.IsFailed)
            return configurationResult.ToResult();
        
        _projectConfigurations.Add(configurationResult.Value);

        return Result.Ok();
    }

    public void Start(CancellationToken ct)
    {
        sw.Start();
        foreach (var projectConfiguration in _projectConfigurations)
        {
            _processes.Add(Run(projectConfiguration));
        }
        
        // Wait to finish. 
        while (_processes.Any(p => !p.HasExited)){}
        
        sw.Stop();
        
        KoshConsole.Info($"Service was running for: {sw.ElapsedMilliseconds}ms");

        if (_processes.Any(p => p.ExitCode == 1))
            throw new Exception("Process exited with 1");
    }

    public void Dispose()
    {
        foreach (var process in _processes)
        {
            Stop(process);
        }
        
        _projectConfigurations.Clear();
        _processes.Clear();
    }
}