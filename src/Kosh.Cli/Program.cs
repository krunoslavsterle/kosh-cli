using Kosh.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.AddCommand<StartCommand>("start");
    //config.AddCommand<LogsCommand>("logs");
    //config.AddCommand<StatusCommand>("status");
});

return app.Run(args);