using System.ComponentModel;
using Kosh.Cli.Rendering;
using Kosh.Core.Constants;
using Kosh.Core.Events;
using Kosh.Runners;
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

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken ct)
    {
        var configDefinitionResult = StartCommandPipeline.Execute(settings);
        if (configDefinitionResult.IsFailed)
            return -1;
        
        var supervisor = new Supervisor.Supervisor(configDefinitionResult.Value, new RunnerFactory());

        // Subscribe to Service events
        supervisor.GroupEvents.Subscribe(runtime =>
        {
            KoshConsole.WriteServiceLog($"{runtime.Definition.Name}-group", runtime.Status.ToString());
        });
        
        // Subscribe to Service events
        supervisor.ServiceEvents.Subscribe(runtime =>
        {
            KoshConsole.WriteServiceLog(runtime.Definition.Name, runtime.Status.ToString());
        });
        
        supervisor.GroupLogs.Subscribe(log =>
        {
            if (log.Type == LogType.Info)
                KoshConsole.WriteServiceLog($"{log.GroupName}-group", log.Line);
            else
                KoshConsole.WriteServiceErrorLog($"{log.GroupName}-group", log.Line);
        });
        
        supervisor.ServiceLogs.Subscribe(log =>
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

            // TODO: HANDLE INPUT HERE.
        }


        return 0;
    }
}