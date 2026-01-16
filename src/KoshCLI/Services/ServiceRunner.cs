using KoshCLI.Config;
using Spectre.Console;

namespace KoshCLI.Services;

internal static class ServiceRunner
{
    public static void StartAll(List<ServiceConfig> services)
    {
        if (services.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No services defined in .koshconfig.[/]");
            return;
        }

        AnsiConsole.MarkupLine("[bold underline]Starting services[/]");

        foreach (var service in services)
        {
            if (service.Type is null || service.Name is null)
            {
                // TODO: MAYBE STOP HERE?
                AnsiConsole.MarkupLine(
                    "[red]Invalid service entry in config (missing name or type). Skipping.[/]"
                );
                continue;
            }

            switch (service.Type.ToLowerInvariant())
            {
                case "dotnet":
                    DotnetServiceRunner.Start(service);
                    break;

                // case "npm":
                //     NpmServiceRunner.Start(service);
                //     break;

                default:
                    AnsiConsole.MarkupLine(
                        $"[red]Unknown service type:[/] {service.Type} for service [bold]{service.Name}[/]. Skipping."
                    );
                    break;
            }
        }
    }
}
