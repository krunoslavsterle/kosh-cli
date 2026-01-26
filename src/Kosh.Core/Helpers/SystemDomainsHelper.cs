using System.Diagnostics;
using System.Runtime.InteropServices;
using FluentResults;
using Kosh.Core.Definitions;

namespace Kosh.Core.Helpers;

public static class SystemDomainsHelper
{
    public static Result EnsureDomainsExists(List<HostDefinition> hosts, OSPlatform osPlatform)
    {
        if (hosts.Count == 0)
            return Result.Ok();

        var hostsFilePath = GetHostsFilePath(osPlatform);
        var hostsContent = File.ReadAllText(hostsFilePath);

        var hostsToInsert = new List<string>();
        foreach (var host in hosts)
        {
            if (!hostsContent.Contains(host.Domain!))
                hostsToInsert.Add(host.Domain!);
        }

        if (hostsToInsert.Any())
        {
            var hostsArgs = string.Join(" ", hostsToInsert);
            //KoshConsole.Info($"Inserting hosts: {hostsArgs} ...");

            try
            {
                if (osPlatform == OSPlatform.Windows)
                    InsertDomainsPs1(hostsToInsert);
                else
                    InsertDomainsBash(hostsArgs);
            }
            catch (Exception e)
            {
                return Result.Fail(e.Message);
            }
        }

        return Result.Ok();
    }

    private static string GetHostsFilePath(OSPlatform osPlatform)
    {
        if (osPlatform == OSPlatform.Windows)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"drivers\etc\hosts"
            );
        }

        return "/etc/hosts";
    }

    private static void InsertDomainsBash(string hostsArgs)
    {
        var exeDir = AppContext.BaseDirectory;
        var scriptPath = Path.Combine(exeDir, "Scripts", "add-hosts.sh");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = $"{scriptPath} {hostsArgs}",
                UseShellExecute = false,
               
            },
        };

        process.Start();
        process.WaitForExit();
    }

    private static void InsertDomainsPs1(List<string> domains)
    {
        var exeDir = AppContext.BaseDirectory;
        var scriptPath = Path.Combine(exeDir, "Scripts", "add-hosts.ps1");
        var args = string.Join(" ", domains.Select(d => $"\"{d}\""));

        var ps1 = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File {scriptPath} {args}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(ps1);

        var output = process!.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Exception($"PowerShell script failed:\n{error}");
        }

    }
}
