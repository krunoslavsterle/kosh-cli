using System.Diagnostics;
using System.Text.Json.Nodes;
using FluentResults;
using Kosh.Core.Definitions;
using Kosh.Core.Runners;

namespace Kosh.Runners.Runner.Docker;

internal sealed class DockerComposeRunner : IRunner
{
    public Task<Result<IRunningProcess>> StartAsync(ServiceDefinition service, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            WorkingDirectory = service.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("compose");

        var args = string.IsNullOrWhiteSpace(service.Args)
            ? "up --remove-orphans"
            : service.Args;

        foreach (var arg in args.ToSplitArgs())
            psi.ArgumentList.Add(arg);

        psi.LoadEnvs(service);

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        try
        {
            if (!process.Start())
                return Task.FromResult(Result.Fail<IRunningProcess>("Failed to start docker process."));
        }
        catch (Exception e)
        {
            return Task.FromResult(Result.Fail<IRunningProcess>($"Failed to start process: {e.Message}"));
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var runningProcess = new RunningProcess(service.Id, process);

        _ = Task.Run(async () =>
        {
            var ready = await WaitForComposeReady(ct, service.WorkingDirectory);
            runningProcess.Ready.TrySetResult(ready ? 1 : 0);
        }, ct);

        return Task.FromResult(Result.Ok<IRunningProcess>(runningProcess));
    }

    private static async Task<bool> WaitForComposeReady(CancellationToken ct, string workingDirectory)
    {
        var expectedServices = GetExpectedServices(workingDirectory);
        var checkCount = 0;

        while (!ct.IsCancellationRequested)
        {
            if (++checkCount > 50)
                return false;

            var containers = GetComposeContainers(workingDirectory);

            if (containers.Count > 0 && containers.All(c => c.State == "running"))
            {
                // If we know the expected services, verify all are present
                if (expectedServices.Count > 0)
                {
                    var runningNames = containers
                        .Select(c => c.Name)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    // Docker Compose container names follow the pattern: <project>-<service>-<replica>
                    // Check that every expected service has at least one matching container
                    if (expectedServices.All(expected =>
                        runningNames.Any(name => name.Contains(expected, StringComparison.OrdinalIgnoreCase))))
                    {
                        return true;
                    }
                }
                else
                {
                    // Fallback: no expected services info, use current behavior
                    return true;
                }
            }

            // Shorter delay on first few checks for already-running containers
            await Task.Delay(checkCount <= 3 ? 100 : 300, ct);
        }

        return false;
    }

    private static List<string> GetExpectedServices(string workingDirectory)
    {
        try
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "compose config --services",
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

            if (p.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return [];

            return output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static List<(string Name, string State)> GetComposeContainers(string workingDirectory)
    {
        try
        {
            using var p = new Process
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

            var result = new List<(string Name, string State)>();
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // NDJSON: multiple JSON objects, one per line
            if (lines.Length > 1)
            {
                foreach (var line in lines)
                {
                    try
                    {
                        var obj = JsonNode.Parse(line) as JsonObject;
                        if (obj != null)
                            result.Add((obj["Name"]?.ToString() ?? "", obj["State"]?.ToString() ?? ""));
                    }
                    catch { /* skip malformed lines */ }
                }
                return result;
            }

            // Single line: could be a JSON array or a single object
            JsonNode? node;
            try
            {
                node = JsonNode.Parse(output);
            }
            catch
            {
                return [];
            }

            if (node is JsonArray arr)
            {
                foreach (var item in arr)
                    result.Add((item?["Name"]?.ToString() ?? "", item?["State"]?.ToString() ?? ""));
                return result;
            }

            if (node is JsonObject singleObj)
            {
                result.Add((singleObj["Name"]?.ToString() ?? "", singleObj["State"]?.ToString() ?? ""));
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