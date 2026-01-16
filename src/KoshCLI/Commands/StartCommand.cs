using KoshCLI.Config;
using KoshCLI.Services;
using KoshCLI.System;
using KoshCLI.Terminal;
using Spectre.Console;
using Spectre.Console.Cli;

namespace KoshCLI.Commands;

public class StartCommand : Command<StartCommand.Settings>
{
    public class Settings : CommandSettings { }

    public override int Execute(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken
    )
    {
        KoshConsole.Info($"Validating kosh project..");

        var osPlatformResult = SystemHelper.GetOsPlatform();
        if (osPlatformResult.IsFailed)
        {
            KoshConsole.Error(osPlatformResult.Errors[0].Message);
            Environment.Exit(1);
        }

        // TODO: THIS IS ONLY FOR TESTING
        var configResult = KoshConfigLoader.Load(
            "/home/krunoslav/Workspace/Work/kosh-test-project"
        );
        if (configResult.IsFailed)
        {
            KoshConsole.Error(configResult.Errors[0].Message);
            Environment.Exit(1);
        }

        var configValidator = new KoshConfigValidator();
        var validationResult = configValidator.Validate(configResult.Value);

        if (!validationResult.IsValid)
        {
            foreach (var error in validationResult.Errors)
                KoshConsole.Error(error.ErrorMessage);

            Environment.Exit(1);
        }

        var commandsValidationResult = SystemCommandsValidator.ValidateConfig(configResult.Value);
        if (!commandsValidationResult.IsValid)
        {
            KoshConsole.Error("Please install the missing tool(s)");

            var table = new Table().AddColumn("Tool").AddColumn("Status");
            table.AddRow(
                "Docker",
                commandsValidationResult.DockerValid ? "[green]OK[/]" : "[red]Missing[/]"
            );

            table.AddRow(
                "Docker Compose",
                commandsValidationResult.DockerComposeValid ? "[green]OK[/]" : "[red]Missing[/]"
            );

            table.AddRow(
                "Caddy",
                commandsValidationResult.ProxyValid ? "[green]OK[/]" : "[red]Missing[/]"
            );

            AnsiConsole.Write(table);
            Environment.Exit(1);
        }

        KoshConsole.Success("Kosh project valid");

        SystemDomainsHelper.EnsureDomainsExists(configResult.Value.Hosts, osPlatformResult.Value);

        AnsiConsole
            .Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.Default)
            .Start(
                $"[yellow]Starting {configResult.Value.ProjectName}[/]",
                ctx =>
                {
                    // MIGRATIONS
                    KoshConsole.Info($"Running migrations...");
                    Thread.Sleep(1000);
                    // TODO: MigrationRunner.Run(config.ModulesPath);
                    KoshConsole.Success($"Running migrations completed");

                    // SERVICES
                    ctx.Spinner(Spinner.Known.BouncingBar);
                    ctx.Status("[bold blue]Starting services[/]");
                    ServiceRunner.StartAll(configResult.Value.Services);
                }
            );

        KoshConsole.Success($"{configResult.Value.ProjectName} ready!.");
        KoshConsole.Empty();

        // TODO: Refactor
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            KoshConsole.Error("Stopping all services...");

            foreach (var p in ServiceRunner.Running)
            {
                try
                {
                    p.Kill(true);
                }
                catch { }
            }
            Environment.Exit(0);
        };

        // TODO: REFACTOR
        while (true)
        {
            Thread.Sleep(200);
        }
    }
}
