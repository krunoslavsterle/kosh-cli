using System.Diagnostics;
using FluentResults;
using Kosh.Core.Definitions;
using Kosh.Core.Runners;

namespace Kosh.Runners.Runner.Dotnet;

internal sealed class DotnetWatchRunner : IRunner
{
    public Task<Result<IRunningProcess>> StartAsync(ServiceDefinition service, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = service.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("watch");

        foreach (var arg in service.Args.ToSplitArgs())
            psi.ArgumentList.Add(arg);

        DotnetHelper.HandleDotnetRootEnv(psi);

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