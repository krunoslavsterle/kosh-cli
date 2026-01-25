using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kosh.Cli.Rendering;

public static class DashboardRenderer
{
    public static IRenderable Render(DashboardState state)
    {
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        var statusTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Service")
            .AddColumn("Status");

        foreach (var kv in state.Statuses)
        {
            var name = kv.Key.Value; // ili mapiraj ID → Name
            var status = kv.Value.ToString();

            statusTable.AddRow(
                state.FocusedService == kv.Key
                    ? $"[yellow]{name}[/]"
                    : name,
                status);
        }

        var logPanel = new Panel(
                string.Join("\n", state.Logs.Reverse().Take(20)))
            .Header("Logs")
            .BorderColor(Color.Grey);

        grid.AddRow(statusTable, logPanel);

        return grid;
    }

}