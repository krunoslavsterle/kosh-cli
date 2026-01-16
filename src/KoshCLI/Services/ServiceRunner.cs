using System.Diagnostics;
using KoshCLI.Config;
using KoshCLI.Terminal;
using Spectre.Console;

namespace KoshCLI.Services;

internal static class ServiceRunner
{
    private static readonly List<Process> _running = new();
    public static IReadOnlyList<Process> Running => _running;

    public static void Register(Process p)
    {
        _running.Add(p);
    }

    public static void StartAll(List<ServiceConfig> services)
    {
        if (services.Count == 0)
        {
            KoshConsole.Info("No services defined in .koshconfig.");
            return;
        }

        foreach (var service in services)
        {
            if (service.Type is null || service.Name is null)
            {
                // TODO: MAYBE STOP HERE?
                KoshConsole.Error(
                    "Invalid service entry in config (missing name or type). Skipping."
                );
                continue;
            }

            switch (service.Type.ToLowerInvariant())
            {
                case "dotnet":
                    DotnetServiceRunner.Start(service);
                    break;

                case "caddy":
                    CaddyServiceRunner.Start(service);
                    break;

                // case "npm":
                //     NpmServiceRunner.Start(service);
                //     break;

                default:
                    KoshConsole.Error(
                        $"Unknown service type:[/] {service.Type} for service [bold]{service.Name}[/]. Skipping."
                    );
                    break;
            }
        }
    }
}
