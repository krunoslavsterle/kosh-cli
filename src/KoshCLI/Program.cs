using KoshCLI.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("kosh");

    config
        .AddCommand<StartCommand>("start")
        .WithDescription("Starts all services defined in .koshconfig");

    // TODO: config.AddCommand<MigrateCommand>("migrate").WithDescription("Runs all migrations");
});

return app.Run(args);
