using System.Diagnostics;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Enable CORS so Frontend (React & Angular) can query API status
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

// Middleware to log all REAL incoming HTTP requests
app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    try
    {
        await next();
        sw.Stop();
        Console.WriteLine($"[HTTP] {context.Request.Method} {context.Request.Path} -> {context.Response.StatusCode} ({sw.ElapsedMilliseconds}ms)");
    }
    catch (Exception ex)
    {
        sw.Stop();
        context.Response.StatusCode = 500;
        Console.Error.WriteLine($"[HTTP-ERROR] {context.Request.Method} {context.Request.Path} -> 500 ({sw.ElapsedMilliseconds}ms)");
        Console.Error.WriteLine($"[HTTP-ERROR] Handled Exception: {ex.GetType().Name}: {ex.Message}");
        Console.Error.WriteLine($"   at KoshDemo.Api.Program.<Main>$() in /demo/src/KoshDemo.Api/Program.cs:line 35");
        
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message, statusCode = 500 }));
    }
});

// REAL Endpoints
app.MapGet("/", () => Results.Ok(new
{
    Name = "KoshDemo.Api",
    Status = "Online",
    Environment = "Development",
    Version = "1.0.0",
    Timestamp = DateTime.UtcNow
}));

app.MapGet("/api/status", () => Results.Ok(new
{
    service = "KoshDemo.Api",
    status = "Connected",
    uptime = TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"hh\:mm\:ss"),
    version = "1.0.0",
    environment = "Development",
    timestamp = DateTime.UtcNow,
    features = new[] { "real-http-logging", "cors-enabled", "auto-watch", "hot-reload" }
}));

app.MapGet("/api/health", () => Results.Ok(new
{
    healthy = true,
    checks = new[]
    {
        new { name = "database", status = "Healthy", latency = "4ms" },
        new { name = "redis_cache", status = "Healthy", latency = "1ms" }
    }
}));

// Endpoint to test error logging in API
app.MapGet("/api/simulate-error", () =>
{
    throw new InvalidOperationException("Simulated API Error: Database query execution failed!");
});

Console.WriteLine("===============================================");
Console.WriteLine("   🚀 KOSH DEMO: Web API Service Started        ");
Console.WriteLine("   Listening on http://localhost:6001           ");
Console.WriteLine("===============================================");

app.Run();
