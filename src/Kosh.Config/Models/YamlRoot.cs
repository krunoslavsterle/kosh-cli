namespace Kosh.Config.Models;

internal class YamlRoot
{
    public string? ProjectName { get; init; }
    public string? Root { get; set; }
    public string? ModulesPath { get; init; }
    public List<YamlHost> Hosts { get; init; } = [];
    public List<YamlService> Services { get; init; } = [];
}

internal class YamlService
{
    public string? Name { get; init; }
    public string? Type { get; init; }
    public string? Path { get; init; }
    public string? Args { get; init; }
    public string? Logs { get; init; }
    public bool InheritEnv { get; init; } = false;
    public Dictionary<string, string> Env { get; init; } = [];
}

internal record YamlHost
{
    public string? Domain { get; init; }
}