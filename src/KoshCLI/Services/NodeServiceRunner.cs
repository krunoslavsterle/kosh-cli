using System.Diagnostics;
using System.Text.Json.Nodes;
using KoshCLI.Config;
using KoshCLI.Terminal;

namespace KoshCLI.Services;

internal class NodeServiceRunner
{
    public static void Start(ServiceConfig service)
    {
        if (service.Path is null)
        {
            // TODO: MAYBE STOP HERE, OR VALIDATE THIS BEFORE!
            KoshConsole.Error($"Service {service.Name}: path is not set.");
            return;
        }

        if (!Directory.Exists(service.Path) && !File.Exists(service.Path))
        {
            KoshConsole.Error($"Service {service.Name}: path '{service.Path}' does not exist.");
            return;
        }

        var args = BuildArguments(service);

        KoshConsole.Info($"Starting node service [bold][[{service.Name}]][/] ...");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "npm",
                Arguments = $"run {args}",
                WorkingDirectory = GetWorkingDirectory(service),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        foreach (var kv in service.Env)
            process.StartInfo.Environment[kv.Key] = kv.Value;

        if (!service.Logs.HasValue || service.Logs.Value)
        {
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    KoshConsole.WriteServiceLog(service.Name!, EscapeMarkup(e.Data));
            };

            process.ErrorDataReceived += (_, e) =>
            {
                // THIS IS CALLED WHEN PROCESS RETURNS 1
                // TODO: DO WE NEED TO STOP THE KOSH PROCESS HERE??
                if (!string.IsNullOrEmpty(e.Data))
                    KoshConsole.WriteServiceErrorLog(service.Name!, EscapeMarkup(e.Data)); // TODO: MAYBE DIFFERENT COLOR
            };
        }

        try
        {
            process.Start();

            ServiceRunner.Register(process);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            KoshConsole.Success($"Service [bold][[{service.Name}]][/] started.");
        }
        catch (Exception ex)
        {
            KoshConsole.Error(
                $"Failed to start dotnet service {service.Name}: {EscapeMarkup(ex.Message)}"
            );
        }
    }

    private static string GetWorkingDirectory(ServiceConfig service)
    {
        if (Directory.Exists(service.Path!))
            return service.Path!;

        var dir = Path.GetDirectoryName(service.Path!);
        return string.IsNullOrEmpty(dir) ? Environment.CurrentDirectory : dir;
    }

    private static string BuildArguments(ServiceConfig service)
    {
        if (string.IsNullOrEmpty(service.Args))
            return "dev";

        return service.Args;
    }

    private static string EscapeMarkup(string input) => input.Replace("[", "[[").Replace("]", "]]");
}
