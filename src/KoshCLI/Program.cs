using KoshCLI.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("kosh");
    
    config
        .AddCommand<StartCommand>("start")
        .WithDescription("Starts all services defined in .koshconfig");
    
    config
        .AddCommand<VersionCommand>("version")
        .WithDescription("Shows current kosh version");
});

return app.Run(args);
