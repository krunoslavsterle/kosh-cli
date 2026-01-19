using FluentResults;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KoshCLI.Config;

internal class KoshConfigLoader
{
    private const string ConfigFile = ".koshconfig";

    public static Result<KoshConfig> Load(string? configPath)
    {
        if (!File.Exists(ConfigFile))
            return Result.Fail(".koshconfig not found. Run kosh from project root.");
        
        configPath = configPath == null ? ConfigFile : Path.Combine(configPath, ConfigFile);
        var configFullPath = Path.GetFullPath(configPath);

        var yaml = File.ReadAllText(configFullPath);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<KoshConfig>(yaml);
        if (config == null!)
        {
            return Result.Fail(".koshconfig file is not formatted properly");
        }

        config.Root ??= Path.GetDirectoryName(configFullPath)!;
        return config;
    }
}
