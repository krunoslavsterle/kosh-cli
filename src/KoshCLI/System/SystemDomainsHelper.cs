using System.Diagnostics;
using System.Runtime.InteropServices;
using KoshCLI.Config;
using KoshCLI.Terminal;
using Spectre.Console;

namespace KoshCLI.System;

internal class SystemDomainsHelper
{
    public static void EnsureDomainsExists(List<HostEntry> hosts, OSPlatform osPlatform)
    {
        if (!hosts.Any())
            return;

        KoshConsole.Info("Ensuring domains exist...");

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
            AnsiConsole.MarkupLine($"[bold yellow] Inserting hosts: {hostsArgs} ...[/]");

            InsertDomainsBash(hostsArgs);
        }

        KoshConsole.Success("Domains exist");
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
                UseShellExecute = false
            },
        };

        process.Start();
        process.WaitForExit();
    }
}
