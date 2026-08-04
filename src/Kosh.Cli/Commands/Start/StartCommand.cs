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

        [CommandOption("--no-tui")]
        [Description("Disables interactive TUI dashboard and streams logs to plain stdout")]
        public bool NoTui { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken ct)
    {
        var configDefinitionResult = StartCommandPipeline.Execute(settings);
        if (configDefinitionResult.IsFailed)
            return -1;

        var supervisor = new Supervisor.Supervisor(configDefinitionResult.Value, new RunnerFactory());

        // Use TUI mode if interactive and --no-tui is not specified
        if (!settings.NoTui && !Console.IsOutputRedirected)
        {
            Terminal.Gui.Application.Init();
            var top = Terminal.Gui.Application.Top;
            var dashboard = new KoshTuiDashboard(configDefinitionResult.Value.ProjectName);
            top.Add(dashboard);

            // Global Key Interception (Evaluated BEFORE focused controls swallow keys)
            Terminal.Gui.Application.RootKeyEvent = (keyEvent) => dashboard.HandleRootKeyEvent(keyEvent);

            ConsoleCancelEventHandler cancelHandler = (_, e) =>
            {
                e.Cancel = true;
                Terminal.Gui.Application.MainLoop?.Invoke(() => Terminal.Gui.Application.RequestStop());
            };
            Console.CancelKeyPress += cancelHandler;

            supervisor.ServiceEvents.Subscribe(runtime =>
            {
                dashboard.UpdateServiceStatus(runtime);
            });

            supervisor.ServiceLogs.Subscribe(log =>
            {
                dashboard.AppendLog(log.ServiceName, log.Line, log.Type == LogType.Error);
            });

            supervisor.GroupLogs.Subscribe(log =>
            {
                dashboard.AppendLog($"{log.GroupName}-group", log.Line, log.Type == LogType.Error);
            });

            // Start services in background
            var startTask = supervisor.StartAllAsync(ct);

            try
            {
                Terminal.Gui.Application.Run();
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
                Terminal.Gui.Application.Shutdown();
                KoshConsole.Info("Stopping all services...");
                await supervisor.StopAllAsync(CancellationToken.None);
                KoshConsole.Success("All services stopped.");
            }

            return 0;
        }

        // Fallback / Plain Text mode (--no-tui or non-interactive)
        supervisor.GroupEvents.Subscribe(runtime =>
        {
            if (!runtime.Definition.IsVirtualGroup)
                KoshConsole.WriteServiceLog($"{runtime.Definition.Name}-group", runtime.Status.ToString());
        });

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

        try
        {
            var result = await supervisor.StartAllAsync(ct);
            if (result.IsFailed)
            {
                KoshConsole.Error(result.Errors[0].Message);
                return -1;
            }

            while (!ct.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);

                    if (key.Key == ConsoleKey.Q)
                        break;
                }
                else
                {
                    await Task.Delay(100, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
        finally
        {
            KoshConsole.Info("Stopping all services...");
            await supervisor.StopAllAsync(CancellationToken.None);
            KoshConsole.Success("All services stopped.");
        }

        return 0;
    }
}