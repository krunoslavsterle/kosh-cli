using FluentResults;
using Kosh.Config.Models;
using Kosh.Core.Constants;
using Kosh.Core.Definitions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Kosh.Config.Parsing;

internal static class ConfigLoader
{
    public static Result<string> Read(string? configPath, ConfigType configType)
    {
        var configFullPath = GetKoshconfigAbsolutePath(configPath, configType);
        if (!File.Exists(configFullPath))
            return Result.Fail($"{ConfigConstants.ConfigFile} not found. Run kosh from project root.");

        return File.ReadAllText(configFullPath);
    }

    public static Result<YamlRoot> Load(string? configPath)
    {
        var yamlResult = Read(configPath, ConfigType.RealConfig);
        if (yamlResult.IsFailed)
            return yamlResult.ToResult<YamlRoot>();

        var yaml = yamlResult.Value;

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<YamlRoot>(yaml);
        if (config == null!)
        {
            return Result.Fail($"{ConfigConstants.ConfigFile} file is not formatted properly");
        }

        var configFullPath = GetKoshconfigAbsolutePath(configPath, ConfigType.RealConfig);
        config.Root ??= Path.GetDirectoryName(configFullPath)!;

        var configValidator = new YamlValidator();
        var validationResult = configValidator.Validate(config);

        if (!validationResult.IsValid)
        {
            return Result.Fail<YamlRoot>(validationResult.Errors.Select(e => e.ErrorMessage));
        }

        return config;
    }

    private static string GetKoshconfigAbsolutePath(string? configPath, ConfigType configType)
    {
        var configName = configType switch
        {
            ConfigType.InitConfig => ConfigConstants.InitConfigFile,
            ConfigType.ExampleConfig => ConfigConstants.ExampleConfigFile,
            _ => ConfigConstants.ConfigFile
        };

        configPath =
            configPath == null
                ? ConfigConstants.ConfigFile
                : Path.Combine(configPath, configName);

        return Path.GetFullPath(configPath);
    }
}