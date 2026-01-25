using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Spectre.Console;

namespace Kosh.Cli.Rendering;

public static class ColorGenerator
{
    private static readonly ConcurrentDictionary<string, Color> Cache = new();

    public static Color FromName(string name)
    {
        return Cache.GetOrAdd(name, GenerateColor(name));
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