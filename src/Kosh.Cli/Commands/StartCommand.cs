using System.ComponentModel;
using Kosh.Cli.Rendering;
using Kosh.Config;
using Kosh.Core.Constants;
using Kosh.Core.Events;
using Kosh.Runners;
using Spectre.Console.Cli;

namespace Kosh.Cli.Commands;

public sealed class StartCommand : AsyncCommand<StartCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-c|--config <PATH>")]
        [Description($"Optional path to a custom {ConfigConstants.ConfigFile}")]
        public string? ConfigPath { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken ct)
    {
        KoshConsole.Info($"Validating kosh project..");

        var configResult = ConfigProcessor.Process(settings.ConfigPath);
        if (configResult.IsFailed)
        {
            KoshConsole.Error(configResult.Errors[0].Message);
            return -1;
        }

        var configDefinition = configResult.Value;

        var supervisor = new Supervisor.Supervisor(configDefinition, new RunnerFactory());

        // Subscribe to events
        supervisor.ServiceEvents.Subscribe(runtime =>
        {
            KoshConsole.WriteServiceLog(runtime.Definition.Name, runtime.Status.ToString());
        });
        
        supervisor.LogEvents.Subscribe(log =>
        {
            if (log.Type == LogType.Info)
                KoshConsole.WriteServiceLog(log.ServiceName, log.Line);
            else
                KoshConsole.WriteServiceErrorLog(log.ServiceName, log.Line);
        });

        var result = await supervisor.StartAllAsync(CancellationToken.None);
        if (result.IsFailed)
        {
            KoshConsole.Error(result.Errors[0].Message);
            return -1;
        }

        while (!ct.IsCancellationRequested)
        {
            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Q)
                break;

            // kasnije: komande, fuzzy search, restart, stop...
        }


        return 0;
    }
}