namespace KoshCLI.Config;

internal class KoshConfig
{
    public string? ProjectName { get; init; }
    public string? Root { get; set; }
    public string? ModulesPath { get; init; }
    public List<HostEntry> Hosts { get; init; } = [];
    public List<ServiceConfig> Services { get; init; } = [];
}

public class ServiceConfig
{
    public string? Name { get; init; }
    public string? Type { get; init; }
    public string? Path { get; init; }
    public string? Args { get; init; }
    public bool Logs { get; init; } = false;
    public bool InheritRootEnv { get; init; } = false;
    public Dictionary<string, string> Env { get; init; } = [];
}

internal record HostEntry
{
    public string? Domain { get; init; }
}
