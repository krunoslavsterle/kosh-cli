using System.Diagnostics;

namespace KoshCLI.System;

public static class SystemCommandChecker
{
    public static bool Exists(string command, string args = "--version")
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
