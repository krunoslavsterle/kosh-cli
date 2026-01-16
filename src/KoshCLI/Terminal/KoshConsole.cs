using Spectre.Console;

namespace KoshCLI.Terminal;

public static class KoshConsole
{
    public static void WriteServiceLine(string service, string message)
    {
        var color = ColorGenerator.FromName(service);
        var prefix = $"[bold {color.ToMarkup()}]{service}[/]";

        AnsiConsole.MarkupLine($"[[{prefix}]]: {message}");
    }

    public static void WriteServiceErrorLine(string service, string message)
    {
        var color = ColorGenerator.FromName(service);
        var prefix = $"[bold {color.ToMarkup()}]{service}[/]";

        AnsiConsole.MarkupLine($"[[{prefix}]][bold red][[!]][/]: {message}");
    }

    public static void Empty() => AnsiConsole.MarkupLine("");

    public static void Info(string message) =>
        AnsiConsole.MarkupLine($"[yellow]> [black on yellow] kosh [/] ! {message}[/]");

    public static void Error(string message) =>
        AnsiConsole.MarkupLine($"[red]> [black on red] kosh [/] ✗ {message}[/]");

    public static void Success(string message) =>
        AnsiConsole.MarkupLine($"[green]> [black on green] kosh [/] ✓ {message}[/]");
}
