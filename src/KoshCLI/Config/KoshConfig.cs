namespace KoshCLI.Config;

internal class KoshConfig
{
    public string? ProjectName { get; init; }
    public string? ModulesPath { get; init; }
    public string? Proxy { get; init; }
    public DockerConfig? Docker { get; init; }
    public List<HostEntry> Hosts { get; init; } = [];
    public List<ServiceConfig> Services { get; init; } = [];
}

internal class ServiceConfig
{
    public string? Name { get; init; }
    public string? Type { get; init; }
    public string? Path { get; init; }
    public bool? Logs { get; init; }
    public string? Args { get; init; }
    public Dictionary<string, string> Env { get; init; } = [];
}

internal record DockerConfig
{
    public string? ComposeFile { get; init; }
    public bool? AutoStart { get; init; }
}

internal record HostEntry
{
    public string? Domain { get; init; }
}
