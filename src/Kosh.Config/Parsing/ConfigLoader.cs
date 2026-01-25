using FluentResults;
using Kosh.Config.Models;
using Kosh.Core.Constants;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Kosh.Config.Parsing;

internal class ConfigLoader
{
    public static Result<YamlRoot> Load(string? configPath)
    {
        configPath =
            configPath == null
                ? ConfigConstants.ConfigFile
                : Path.Combine(configPath, ConfigConstants.ConfigFile);

        var configFullPath = Path.GetFullPath(configPath);
        if (!File.Exists(configFullPath))
            return Result.Fail($"{ConfigConstants.ConfigFile} not found. Run kosh from project root.");

        var yaml = File.ReadAllText(configFullPath);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<YamlRoot>(yaml);
        if (config == null!)
        {
            return Result.Fail($"{ConfigConstants.ConfigFile} file is not formatted properly");
        }

        config.Root ??= Path.GetDirectoryName(configFullPath)!;

        var configValidator = new YamlValidator();
        var validationResult = configValidator.Validate(config);

        if (!validationResult.IsValid)
        {
            return Result.Fail<YamlRoot>(validationResult.Errors.Select(e => e.ErrorMessage));
        }

        return config;
    }
}