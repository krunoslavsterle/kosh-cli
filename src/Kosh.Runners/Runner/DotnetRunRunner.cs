using System.Diagnostics;
using Kosh.Core.Definitions;
using Kosh.Core.Runners;

namespace Kosh.Runners.Runner;

internal sealed class DotnetRunRunner : IRunner
{
    public async Task<IRunningProcess> StartAsync(ServiceDefinition service, CancellationToken ct)
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

       
        if (!string.IsNullOrWhiteSpace(service.Args))
        {
            foreach (var part in SplitArgs(service.Args))
                psi.ArgumentList.Add(part);
        }
        
        // TODO: HANDLE LINUX CASE
        // if (!OperatingSystem.IsWindows())
        // {
        //     psi.Environment["DOTNET_ROOT"] = GetDotnetRoot();
        //     psi.Environment["PATH"] =
        //         psi.Environment["DOTNET_ROOT"] + ":" + Environment.GetEnvironmentVariable("PATH");
        // }

        psi.LoadEnvs(service.Environment, service.WorkingDirectory);
        
        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        try
        {
            if (!process.Start())
                return null!;
        }
        catch
        {
            return null!;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return new RunningProcess(service.Id, process);
    }

    private static IEnumerable<string> SplitArgs(string args)
    {
        // Minimal, deterministic arg splitter
        return args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}
