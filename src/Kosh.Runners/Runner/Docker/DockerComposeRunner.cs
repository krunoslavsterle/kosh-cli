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
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("compose");

        var args = string.IsNullOrWhiteSpace(service.Args)
            ? "up --remove-orphans"
            : service.Args;

        foreach (var arg in args.ToSplitArgs())
            psi.ArgumentList.Add(arg);

        psi.LoadEnvs(service.Environment, service.WorkingDirectory);

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        try
        {
            if (!process.Start())
                return null!;
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

    // TODO: This is a temp solution. Implement readiness check based on `docker compose config --services`
    private Task<bool> WaitForComposeReady(CancellationToken ct, string workingDirectory)
    {
        var lastCount = -1;
        var checkCount = 0;

        while (!ct.IsCancellationRequested)
        {
            if (++checkCount > 50)
            {
                Console.WriteLine("Waiting for docker compose up for too long!");
                return Task.FromResult(false);
            }

            var containers = GetComposeContainers(workingDirectory);

            if (containers.Count == 0)
            {
                Thread.Sleep(300);
                continue;
            }

            if (lastCount != -1 && containers.Count == lastCount)
            {
                if (containers.All(c => c.State == "running"))
                    return Task.FromResult(false);
            }

            lastCount = containers.Count;
            Thread.Sleep(300);
        }

        return Task.FromResult(false);
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