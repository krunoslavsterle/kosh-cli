namespace KoshCLI;

public static class Constants
{
    public const string ConfigFile = "koshconfig.yaml";
    public const string ExampleConfigFile = "koshconfig.example.yaml";

    public static string[] ServiceTypes =
    [
        "dotnet-watch",
        "dotnet-run",
        "caddy",
        "docker-compose",
        "node",
    ];
}
