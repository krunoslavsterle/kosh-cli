using KoshCLI.Terminal;
using Spectre.Console.Cli;

namespace KoshCLI.Commands;

public class VersionCommand : Command<VersionCommand.VersionSettings>
{
    public class VersionSettings : CommandSettings{}
    
    public override int Execute(CommandContext context, VersionSettings settings, CancellationToken cancellationToken)
    {
        var version = typeof(Program).Assembly
            .GetName()
            .Version?
            .ToString() ?? "unknown";

        KoshConsole.Info($"{version}");
        return 0;
    }
}
