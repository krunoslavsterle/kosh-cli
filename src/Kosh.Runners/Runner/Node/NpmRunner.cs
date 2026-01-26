using System.Diagnostics;
using FluentResults;
using Kosh.Core.Definitions;
using Kosh.Core.Runners;

namespace Kosh.Runners.Runner.Node;

internal sealed class NpmRunner : IRunner
{
    public Task<Result<IRunningProcess>> StartAsync(ServiceDefinition service, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "npm",
            WorkingDirectory = service.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("run");
        
        foreach (var arg in service.Args.ToSplitArgs())
            psi.ArgumentList.Add(arg);
        
        if (string.IsNullOrWhiteSpace(service.Args))
            psi.ArgumentList.Add("dev");
        
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

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return Task.FromResult(Result.Ok<IRunningProcess>(new RunningProcess(service.Id, process))); 
    }
}