using System.Diagnostics;
using System.Text.Json.Nodes;
using FluentResults;
using KoshCLI.Config;
using KoshCLI.Helpers;
using KoshCLI.Terminal;

namespace KoshCLI.Services.Docker;

internal class DockerComposeServiceRunner : IServiceRunner
{
    private readonly ServiceConfig _serviceConfig;
    private readonly string _workingDirectory;
    private Process? _process;

    public DockerComposeServiceRunner(ServiceConfig config, string rootDirectory)
    {
        _serviceConfig = config;
        _workingDirectory = Path.GetFullPath(Path.Combine(rootDirectory, _serviceConfig.Path!));
    }

    public Result Setup()
    {
        return Result.Ok();
    }

    public void Start(CancellationToken ct)
    {
        var args = string.IsNullOrWhiteSpace(_serviceConfig.Args)
            ? "up --remove-orphans"
            : _serviceConfig.Args;

        KoshConsole.Info($"Starting docker-compose service [bold][[{_serviceConfig.Name}]][/] ...");

        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"compose {args} -d",
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.LoadEnvs(_serviceConfig, _workingDirectory);

        _process= new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.SetupConsoleLogs(_serviceConfig, errorLogsByDefault: true);

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        // BLOCKING readiness check
        WaitForComposeReady(ct);

        KoshConsole.Success($"Service [bold][[{_serviceConfig.Name}]][/] started.");
    }

    public void Dispose()
    {
        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(true);
                _process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                KoshConsole.WriteServiceErrorLog(
                    _serviceConfig.Name!,
                    $"Failed to stop process: {ex.Message}"
                );
            }
        }
        
        _process?.Dispose();
        _process = null;
    }

    private void WaitForComposeReady(CancellationToken ct)
    {
        int lastCount = -1;
        int checkCount = 0;

        while (!ct.IsCancellationRequested)
        {
            if (++checkCount > 50)
            {
                KoshConsole.Error("Waiting for docker compose up for too long!");
                Environment.Exit(1);
            }

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