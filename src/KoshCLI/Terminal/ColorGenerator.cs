using System.Security.Cryptography;
using System.Text;
using Spectre.Console;

namespace KoshCLI.Terminal;

internal static class ColorGenerator
{
    private static readonly Dictionary<string, Color> Cache = new();

    public static Color FromName(string name)
    {
        if (Cache.TryGetValue(name, out var cached))
            return cached;

        var color = GenerateColor(name);
        Cache[name] = color;
        return color;
    }

    private static Color GenerateColor(string name)
    {
        using var sha = SHA1.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(name));

        int r = 100 + (hash[0] % 156);
        int g = 100 + (hash[1] % 156);
        int b = 100 + (hash[2] % 156);

        return new Color((byte)r, (byte)g, (byte)b);
    }
}
