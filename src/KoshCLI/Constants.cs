namespace KoshCLI;

public static class Constants
{
    public const string ConfigFile = "koshconfig.yaml";
    public const string ExampleConfigFile = "koshconfig.example.yaml";
    public const string InitConfigFile = "koshconfig.init.yaml";
    public const string EnvFile = ".env";

    public static string[] ServiceTypes =
    [
        "dotnet-watch",
        "dotnet-watch-alt",
        "dotnet-run",
        "caddy",
        "docker-compose",
        "node",
    ];
}
