using System.Diagnostics;
using Kosh.Core.State;

namespace Kosh.Runners.Runner.Dotnet;

public static class DotnetHelper
{
    public static void HandleDotnetRootEnv(ProcessStartInfo psi)
    {
        if (OperatingSystem.IsWindows()) 
            return;
        
        psi.Environment["DOTNET_ROOT"] = GetDotnetRoot();
        psi.Environment["PATH"] =
            psi.Environment["DOTNET_ROOT"] + ":" + Environment.GetEnvironmentVariable("PATH");
    }

    private static string GetDotnetRoot()
    {
        if (GlobalState.DotnetRoot != null)
            return GlobalState.DotnetRoot;

        var dotnetPath = Process
            .Start(
                new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "dotnet",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                }
            )!
            .StandardOutput.ReadToEnd()
            .Trim();

        // TODO EXCEPTION HANDLING
        GlobalState.DotnetRoot = Path.GetDirectoryName(Path.GetDirectoryName(dotnetPath))!;
        return GlobalState.DotnetRoot;
    }
}