using System.Diagnostics;
using System.Text.Json.Nodes;
using FluentResults;
using KoshCLI.Config;
using KoshCLI.Terminal;

namespace KoshCLI.Services.Docker;

internal class DockerComposeServiceRunner : IServiceRunner
{
    private readonly ServiceConfig _serviceConfig;
    private readonly string _workingDirectory;

    public DockerComposeServiceRunner(ServiceConfig config, string rootDirectory)
    {
        _serviceConfig = config;
        _workingDirectory = Path.GetFullPath(Path.Combine(rootDirectory, _serviceConfig.Path!));
    }

    public bool ShouldStopOnExit { get; private set; }

    public Result Setup()
    {
        ShouldStopOnExit = false;
        return Result.Ok();
    }

    public void Start(CancellationToken ct)
    {
        var args = string.IsNullOrWhiteSpace(_serviceConfig.Args) ? "up" : _serviceConfig.Args;

        KoshConsole.Info($"Starting docker-compose service [bold][[{_serviceConfig.Name}]][/] ...");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"compose {args} -d",
                WorkingDirectory = _workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        if (_serviceConfig.ShouldLog)
        {
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null)
                    return;

                KoshConsole.WriteServiceLog(_serviceConfig.Name!, e.Data);
            };
        }
        
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
                return;

            KoshConsole.WriteServiceErrorLog(_serviceConfig.Name!, e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // BLOCKING readiness check
        WaitForComposeReady(ct);

        KoshConsole.Success($"Service [bold][[{_serviceConfig.Name}]][/] started.");
    }

    public void Dispose()
    {
        // TODO: IMPLEMENT DISPOSE METHOD.
        KoshConsole.WriteServiceErrorLog(_serviceConfig.Name!, "Dispose method called!");
    }

    private void WaitForComposeReady(CancellationToken ct)
    {
        int lastCount = -1;

        while (!ct.IsCancellationRequested)
        {
            var containers = GetComposeContainers(_workingDirectory);

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
                return [];

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

            return [];
        }
        catch
        {
            return [];
        }
    }
}
