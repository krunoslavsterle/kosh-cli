using Kosh.Core.Constants;

namespace Kosh.Core.Helpers;

public class EnvHelper
{
    public static Dictionary<string, string> LoadEnvFile(string? path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        path = path == null ? ConfigConstants.EnvFile : Path.Combine(path, ConfigConstants.EnvFile);

        if (!File.Exists(path))
            return result;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            // Must contain '='
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            // Remove surrounding quotes if present
            if (
                (value.StartsWith("\"") && value.EndsWith("\""))
                || (value.StartsWith("'") && value.EndsWith("'"))
            )
            {
                value = value[1..^1];
            }

            result[key] = value;
        }

        return result;
    }
}