using Kosh.Api.Endpoints;
using Kosh.Core.Logs;
using Kosh.Core.Supervisor;
using Microsoft.Extensions.FileProviders;

namespace Kosh.Api;

public class ApiHost
{
    public static IHost Start(
        ISupervisor supervisor,
        LogRingBuffer buffer,
        CancellationToken ct,
        int port = 7777
    )
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.ConfigureKestrel(options => { options.ListenLocalhost(port); });

        builder.Services.AddSingleton(supervisor);
        builder.Services.AddSingleton(buffer);

        var app = builder.Build();

        app.MapStatusEndpoints();
        app.MapStopServiceEndpoint();
        app.MapStartServiceEndpoint();

        // Serve frontend
        var dashboardPath = Path.Combine(AppContext.BaseDirectory, "dashboard");

        if (Directory.Exists(dashboardPath))
        {
            // 1) Default file (index.html)
            app.UseDefaultFiles(
                new DefaultFilesOptions
                {
                    FileProvider = new PhysicalFileProvider(dashboardPath),
                    RequestPath = "",
                }
            );

            // 2) Static files
            app.UseStaticFiles(
                new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(dashboardPath),
                    RequestPath = "",
                }
            );
        }

        // MapEndpoints(app);
        app.RunAsync(ct);
        return app;
    }
}