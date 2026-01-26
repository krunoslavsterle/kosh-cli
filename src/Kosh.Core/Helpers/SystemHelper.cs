using System.Diagnostics;
using System.Runtime.InteropServices;
using FluentResults;

namespace Kosh.Core.Helpers;

public static class SystemHelper
{
    public static Result<OSPlatform> GetOsPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return OSPlatform.Windows;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return OSPlatform.Linux;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return OSPlatform.OSX;

        return Result.Fail("This OS is not supported");
    }
    
    public static bool CheckSystemCommandExistence(string command, string args = "--version")
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();
            process.WaitForExit(100);

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}