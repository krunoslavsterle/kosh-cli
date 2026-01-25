namespace Kosh.Core.Constants;

public static class ConfigConstants
{
    public const string ConfigFile = "koshconfig.yaml";
    public const string ExampleConfigFile = "koshconfig.example.yaml";
    public const string InitConfigFile = "koshconfig.init.yaml";

    public static readonly string[] ServiceTypes =
    [
        "dotnet-watch",
        "dotnet-watch-alt",
        "dotnet-run",
        "caddy",
        "docker-compose",
        "node",
    ];
}