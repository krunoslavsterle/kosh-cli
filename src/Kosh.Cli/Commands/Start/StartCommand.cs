using System.ComponentModel;
using Kosh.Api;
using Kosh.Cli.Rendering;
using Kosh.Core.Constants;
using Kosh.Core.Logs;
using Kosh.Core.Runtime;
using Kosh.Runners;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Kosh.Cli.Commands.Start;

public sealed class StartCommand : AsyncCommand<StartCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-c|--config <PATH>")]
        [Description($"Optional path to a custom {ConfigConstants.ConfigFile}")]
        public string? ConfigPath { get; set; }
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken ct
    )
    {
        AnsiConsole.Write(
            new Panel("KOSH START")
                .Border(BoxBorder.Double)
                .BorderStyle(new Style(Color.Grey))
                .Padding(1, 1)
                .Expand()
        );

        var configDefinitionResult = StartCommandPipeline.Execute(settings);
        if (configDefinitionResult.IsFailed)
            return -1;

        KoshConsole.Success("Loaded configuration: [grey]koshconfig.yaml[/].");

        var buffer = new LogRingBuffer();
        var supervisor = new Supervisor.Supervisor(
            configDefinitionResult.Value,
            new RunnerFactory()
        );

        ApiHost.Start(supervisor, buffer, ct);

        var result = await supervisor.StartAllAsync(CancellationToken.None);
        if (result.IsFailed)
        {
            KoshConsole.Error(result.Errors[0].Message);
            return -1;
        }

        KoshConsole.Success("Supervisor initialized.");
        KoshConsole.Success("Web dashboard running at: [underline blue]http://localhost:7777[/]");

        await AnsiConsole
            .Live(RenderServiceTable(supervisor.Services.Values.ToList()))
            .StartAsync(async ctx =>
            {
                while (!ct.IsCancellationRequested)
                {
                    ctx.UpdateTarget(RenderServiceTable(supervisor.Services.Values.ToList()));
                    await Task.Delay(1000, ct);
                }
            });

        AnsiConsole.MarkupLine(
            "[grey]Open the dashboard for logs, filtering, search and service control.[/]"
        );
        AnsiConsole.MarkupLine("Press [bold]Ctrl+C[/] to stop Kosh.");

        await Task.Delay(Timeout.Infinite, ct);
        return 0;
    }

    private Table RenderServiceTable(IReadOnlyList<ServiceRuntime> services)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Service")
            .AddColumn("Status")
            .AddColumn("PID")
            .AddColumn("Uptime");

        foreach (var svc in services)
        {
            var uptime = DateTimeOffset.UtcNow - svc.StartedAt;

            table.AddRow(
                svc.Definition.Name,
                svc.Status switch
                {
                    ServiceStatus.Running => "[green]Running[/]",
                    ServiceStatus.Starting => "[yellow]Starting[/]",
                    ServiceStatus.NotStarted => "[yellow]Not Started[/]",
                    ServiceStatus.Ready => "[yellow]Ready[/]",
                    ServiceStatus.Failed => "[red]Crashed[/]",
                    _ => "[grey]Unknown[/]",
                },
                svc.Process?.Pid.ToString() ?? "-",
                uptime?.ToString(@"hh\:mm\:ss") ?? "-"
            );
        }

        return table;
    }
}
