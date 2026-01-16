using System.Diagnostics;
using KoshCLI.Config;
using KoshCLI.Terminal;
using Spectre.Console;

namespace KoshCLI.Services;

internal static class DotnetServiceRunner
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

        KoshConsole.Info($"Starting dotnet service [bold][[{service.Name}]][/] ...");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = args,
                WorkingDirectory = GetWorkingDirectory(service),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };

        foreach (var kv in service.Env)
            process.StartInfo.Environment[kv.Key] = kv.Value;

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                KoshConsole.WriteServiceLine(service.Name!, EscapeMarkup(e.Data));
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                KoshConsole.WriteServiceLine(service.Name!, EscapeMarkup(e.Data)); // TODO: MAYBE DIFFERENT COLOR
        };

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
            KoshConsole.Error($"Failed to start dotnet service {service.Name}: {EscapeMarkup(ex.Message)}");
        }
    }

    private static string BuildArguments(ServiceConfig service)
    {
        var baseArgs = "watch --no-hot-reload run";

        if (service.Path!.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            baseArgs = $"{baseArgs} --project \"{service.Path}\"";

        return service.Args is null ? baseArgs : $"{baseArgs} {service.Args}";
    }

    private static string GetWorkingDirectory(ServiceConfig service)
    {
        if (Directory.Exists(service.Path!))
            return service.Path!;

        var dir = Path.GetDirectoryName(service.Path!);
        return string.IsNullOrEmpty(dir) ? Environment.CurrentDirectory : dir;
    }

    private static string EscapeMarkup(string input) => input.Replace("[", "[[").Replace("]", "]]");
}
