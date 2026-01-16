using FluentResults;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KoshCLI.Config;

internal class KoshConfigLoader
{
    private const string ConfigFile = ".koshconfig";

    public static Result<KoshConfig> Load(string src)
    {
        var path = $"{src}/{ConfigFile}";
        if (!File.Exists(path))
            return Result.Fail(".koshconfig not found. Run kosh from project root.");

        var yaml = File.ReadAllText(path);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<KoshConfig>(yaml);
        if (config == null!)
        {
            return Result.Fail(".koshconfig file is not formatted properly");
        }

        return config;
    }
}
