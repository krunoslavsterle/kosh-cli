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

        // Use TUI mode if --no-tui is not specified
        if (!settings.NoTui)
        {
            try
            {
                using var app = Terminal.Gui.App.Application.Create();
                app.Init();
                using var dashboard = new KoshTuiDashboard(app, configDefinitionResult.Value, supervisor);

                var disposables = new List<IDisposable>();

                ConsoleCancelEventHandler cancelHandler = (_, e) =>
                {
                    e.Cancel = true;
                    app.Invoke(() => app.RequestStop());
                };
                Console.CancelKeyPress += cancelHandler;

                disposables.Add(supervisor.ServiceEvents.Subscribe(runtime =>
                {
                    dashboard.UpdateServiceStatus(runtime);
                }));

                disposables.Add(supervisor.ServiceLogs.Subscribe(log =>
                {
                    dashboard.AppendLog(log.ServiceName, log.Line, log.Type == LogType.Error);
                }));

                disposables.Add(supervisor.GroupLogs.Subscribe(log =>
                {
                    dashboard.AppendLog($"{log.GroupName}-group", log.Line, log.Type == LogType.Error);
                }));

                // Start services in background
                var startTask = supervisor.StartAllAsync(ct);

                try
                {
                    app.Run(dashboard);
                }
                finally
                {
                    foreach (var d in disposables) d.Dispose();
                    Console.CancelKeyPress -= cancelHandler;
                    KoshConsole.Info("Stopping all services...");
                    await supervisor.StopAllAsync(CancellationToken.None);
                    KoshConsole.Success("All services stopped.");
                }

                return 0;
            }
            catch (Exception ex)
            {
                KoshConsole.Info($"TUI unavailable ({ex.Message}), falling back to plain text mode.");
            }
        }

        // Fallback / Plain Text mode (--no-tui or non-interactive)
        var disposablesFallback = new List<IDisposable>();

        disposablesFallback.Add(supervisor.GroupEvents.Subscribe(runtime =>
        {
            if (!runtime.Definition.IsVirtualGroup)
                KoshConsole.WriteServiceLog($"{runtime.Definition.Name}-group", runtime.Status.ToString());
        }));

        disposablesFallback.Add(supervisor.ServiceEvents.Subscribe(runtime =>
        {
            KoshConsole.WriteServiceLog(runtime.Definition.Name, runtime.Status.ToString());
        }));

        disposablesFallback.Add(supervisor.GroupLogs.Subscribe(log =>
        {
            if (log.Type == LogType.Info)
                KoshConsole.WriteServiceLog($"{log.GroupName}-group", log.Line);
            else
                KoshConsole.WriteServiceErrorLog($"{log.GroupName}-group", log.Line);
        }));

        disposablesFallback.Add(supervisor.ServiceLogs.Subscribe(log =>
        {
            if (log.Type == LogType.Info)
                KoshConsole.WriteServiceLog(log.ServiceName, log.Line);
            else
                KoshConsole.WriteServiceErrorLog(log.ServiceName, log.Line);
        }));

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
            foreach (var d in disposablesFallback) d.Dispose();
            KoshConsole.Info("Stopping all services...");
            await supervisor.StopAllAsync(CancellationToken.None);
            KoshConsole.Success("All services stopped.");
        }

        return 0;
    }
}