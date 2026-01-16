using KoshCLI.Config;
using KoshCLI.Services;
using KoshCLI.System;
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
        AnsiConsole.MarkupLine("[bold green]Kosh starting...[/]");

        var osPlatformResult = SystemHelper.GetOsPlatform();
        if (osPlatformResult.IsFailed)
        {
            AnsiConsole.MarkupLine($"[bold red] {osPlatformResult.Errors[0].Message}[/]");
            Environment.Exit(1);
        }

        // TODO: THIS IS ONLY FOR TESTING
        var configResult = KoshConfigLoader.Load("/home/krunoslav/Workspace/test/kosh-test");
        if (configResult.IsFailed)
        {
            AnsiConsole.MarkupLine($"[bold red] {configResult.Errors[0].Message}[/]");
            Environment.Exit(1);
        }

        var configValidator = new KoshConfigValidator();
        var validationResult = configValidator.Validate(configResult.Value);

        if (!validationResult.IsValid)
        {
            foreach (var error in validationResult.Errors)
                AnsiConsole.MarkupLine($"[bold red] {error.ErrorMessage}[/]");

            Environment.Exit(1);
        }

        var commandsValidationResult = SystemCommandsValidator.ValidateConfig(configResult.Value);
        if (!commandsValidationResult.IsValid)
        {
            AnsiConsole.MarkupLine("[bold red] Please install the missing tool(s)[/]");

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

        SystemDomainsHelper.EnsureDomainsExists(configResult.Value.Hosts, osPlatformResult.Value);

        AnsiConsole.MarkupLine($"[bold yellow]Starting {configResult.Value.ProjectName}...[/]");

        AnsiConsole.MarkupLine("[yellow]Running migrations...[/]");
        // TODO: MigrationRunner.Run(config.ModulesPath);

        AnsiConsole.MarkupLine("[yellow]Starting services...[/]");
        ServiceRunner.StartAll(configResult.Value.Services);

        AnsiConsole.MarkupLine("[bold green]All done.[/]");
        
        // TODO: Refactor
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; AnsiConsole.MarkupLine("[red]Stopping all services...[/]"); 
            foreach (var p in ServiceRunner.Running) 
            {
                try
                {
                    p.Kill(true); 
                    
                } catch {} } Environment.Exit(0);
        }; 
        
        // TODO: REFACTOR
        while (true) { Thread.Sleep(200); }
    }
}
