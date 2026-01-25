using System.Diagnostics;
using FluentResults;
using Kosh.Core.Definitions;
using Kosh.Core.Runners;

namespace Kosh.Runners.Runner.Docker;

internal sealed class DockerComposeRunner : IRunner
{
    public Task<Result<IRunningProcess>> StartAsync(ServiceDefinition service, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            WorkingDirectory = service.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("compose");
        
        var args = string.IsNullOrWhiteSpace(service.Args)
            ? "up --remove-orphans"
            : service.Args;
        
        foreach (var arg in args.ToSplitArgs())
            psi.ArgumentList.Add(arg);

        psi.LoadEnvs(service.Environment, service.WorkingDirectory);

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        
        try
        {
            if (!process.Start())
                return null!;
        }
        catch (Exception e)
        {
            return Task.FromResult(Result.Fail<IRunningProcess>($"Failed to start process: {e.Message}"));
        }
        
        // // BLOCKING readiness check
        // WaitForComposeReady(ct);
        
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        return Task.FromResult(Result.Ok<IRunningProcess>(new RunningProcess(service.Id, process))); 
    }
}