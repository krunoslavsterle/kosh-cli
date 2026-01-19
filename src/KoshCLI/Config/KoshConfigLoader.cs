using FluentResults;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KoshCLI.Config;

internal class KoshConfigLoader
{
    public static Result<KoshConfig> Load(string? configPath)
    {
        configPath =
            configPath == null
                ? Constants.ConfigFile
                : Path.Combine(configPath, Constants.ConfigFile);

        var configFullPath = Path.GetFullPath(configPath);
        if (!File.Exists(configFullPath))
            return Result.Fail($"{Constants.ConfigFile} not found. Run kosh from project root.");

        var yaml = File.ReadAllText(configFullPath);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<KoshConfig>(yaml);
        if (config == null!)
        {
            return Result.Fail($"{Constants.ConfigFile} file is not formatted properly");
        }

        config.Root ??= Path.GetDirectoryName(configFullPath)!;
        return config;
    }
}
