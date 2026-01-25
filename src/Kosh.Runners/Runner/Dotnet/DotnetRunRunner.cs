using System.Diagnostics;
using FluentResults;
using Kosh.Core.Definitions;
using Kosh.Core.Runners;

namespace Kosh.Runners.Runner.Dotnet;

internal sealed class DotnetRunRunner : IRunner
{
    public async Task<Result<IRunningProcess>> StartAsync(ServiceDefinition service, CancellationToken ct)
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

        psi.ArgumentList.Add("run");
        
        // TODO: IMPLEMENT THIS FOR ALTERNATIVE DOTNET-WATCH
        // if (!withBuild)
        //     args = $"{args} --no-build";

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
            return Result.Fail($"Failed to start process: {e.Message}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return new RunningProcess(service.Id, process);
    }

}
