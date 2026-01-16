using System.Diagnostics;
using System.Text.Json.Nodes;
using KoshCLI.Config;
using KoshCLI.Terminal;
using Spectre.Console;

namespace KoshCLI.Services;

internal static class DockerComposeRunner
{
    public static void Start(ServiceConfig service, CancellationToken token)
    {
        var args = string.IsNullOrWhiteSpace(service.Args) ? "up" : service.Args;

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"compose {args}",
                WorkingDirectory = service.Path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        if (service.ShouldLog)
        {
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null)
                    return;

                KoshConsole.WriteServiceLog(service.Name!, e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null)
                    return;

                KoshConsole.WriteServiceErrorLog(service.Name!, e.Data);
            };
        }

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // BLOCKING readiness check
        WaitForComposeReady(service, token);

        KoshConsole.Success($"Service [bold][[{service.Name}]][/] started.");
    }

    private static void WaitForComposeReady(ServiceConfig service, CancellationToken token)
    {
        int lastCount = -1;

        while (!token.IsCancellationRequested)
        {
            var containers = GetComposeContainers(service.Path!);

            if (containers.Count == 0)
            {
                Thread.Sleep(300);
                continue;
            }

            if (lastCount != -1 && containers.Count == lastCount)
            {
                if (containers.All(c => c.State == "running"))
                    return;
            }

            lastCount = containers.Count;
            Thread.Sleep(300);
        }
    }

    private static List<(string Name, string State)> GetComposeContainers(string workingDirectory)
    {
        try
        {
            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "compose ps --format json",
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            p.Start();
            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();

            if (string.IsNullOrWhiteSpace(output))
                return new();

            JsonNode? node;
            try
            {
                node = JsonNode.Parse(output);
            }
            catch
            {
                return new();
            }

            var result = new List<(string Name, string State)>();

            // CASE 1: array
            if (node is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    result.Add((item?["Name"]?.ToString() ?? "", item?["State"]?.ToString() ?? ""));
                }

                return result;
            }

            // CASE 2: single object
            if (node is JsonObject obj)
            {
                result.Add((obj["Name"]?.ToString() ?? "", obj["State"]?.ToString() ?? ""));

                return result;
            }

            return new();
        }
        catch
        {
            return new();
        }
    }
}
