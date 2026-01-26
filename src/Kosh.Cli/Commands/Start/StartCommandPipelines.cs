using FluentResults;
using Kosh.Cli.Rendering;
using Kosh.Config;
using Kosh.Core.Definitions;
using Kosh.Core.Helpers;
using Spectre.Console;

namespace Kosh.Cli.Commands.Start;

public static class StartCommandPipeline
{
    public static Result<ConfigDefinition> Execute(StartCommand.Settings settings)
    {
        KoshConsole.Info($"Validating kosh project..");
        
        var configResult = ConfigProcessor.Process(settings.ConfigPath);
        if (configResult.IsFailed)
        {
            KoshConsole.Error(configResult.Errors[0].Message);
            return Result.Fail("Fail");
        }

        var configDefinition = configResult.Value;
        
        var commandsValidationResult = SystemCommandsValidator.ValidateConfig(configDefinition);
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
            return Result.Fail("Fail");
        }
        
        KoshConsole.Success("Kosh project valid");
        KoshConsole.Empty();
        
        var domainsResult = SystemDomainsHelper.EnsureDomainsExists(configDefinition.Hosts, configDefinition.OsPlatform);
        if (domainsResult.IsFailed)
        {
            KoshConsole.Error(domainsResult.Errors[0].Message);
            Result.Fail("Fail");
        }
        
        return configDefinition;
    }
}