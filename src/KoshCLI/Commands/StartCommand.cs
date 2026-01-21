using System.ComponentModel;
using KoshCLI.Config;
using KoshCLI.Helpers;
using KoshCLI.Services;
using KoshCLI.System;
using KoshCLI.Terminal;
using Spectre.Console;
using Spectre.Console.Cli;

namespace KoshCLI.Commands;

public class StartCommand : Command<StartCommand.StartSettings>
{
    public static Dictionary<string, string> GlobalEnv { get; } = [];

    public class StartSettings : CommandSettings
    {
        [CommandOption("-c|--config <PATH>")]
        [Description($"Optional path to a custom {Constants.ConfigFile}")]
        public string? ConfigPath { get; set; }
    }

    public override int Execute(
        CommandContext context,
        StartSettings settings,
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

        var configResult = KoshConfigLoader.Load(settings.ConfigPath);
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

        var globalEnvs = EnvHelpers.LoadEnvFile(settings.ConfigPath);
        foreach (var env in globalEnvs)
            GlobalEnv.TryAdd(env.Key, env.Value);

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

        ServiceExecutionManager.StartAll(configResult.Value.Services, configResult.Value.Root!);

        KoshConsole.Success($"{configResult.Value.ProjectName} ready!.");
        KoshConsole.Empty();

        // TODO: Refactor
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            KoshConsole.Error("Stopping all services...");
            ServiceExecutionManager.StopAll();
            Thread.Sleep(500);
            Environment.Exit(0);
        };

        // TODO: REFACTOR
        while (true)
        {
            Thread.Sleep(200);
        }
    }
}
