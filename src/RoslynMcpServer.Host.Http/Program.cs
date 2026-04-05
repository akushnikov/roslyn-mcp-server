using RoslynMcpServer.Infrastructure.DependencyInjection;
using RoslynMcpServer.Mcp;
using RoslynMcpServer.Host.Http;
using Serilog;
using Serilog.Events;

var options = HttpHostOptions.Parse(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var webBuilder = WebApplication.CreateBuilder(args);
    webBuilder.WebHost.UseUrls(options.Urls);

    var logDirectory = Path.Combine(webBuilder.Environment.ContentRootPath, "logs");
    Directory.CreateDirectory(logDirectory);

    webBuilder.Host.UseSerilog((_, _, loggerConfiguration) => loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.Hosting", LogEventLevel.Information)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine(logDirectory, "http-server-.log"),
            rollingInterval: RollingInterval.Day,
            rollOnFileSizeLimit: true,
            retainedFileCountLimit: 14,
            shared: true,
            flushToDiskInterval: TimeSpan.FromSeconds(1)));

    webBuilder.Services.AddRoslynMcpServer();
    webBuilder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithRoslynMcpServer();

    var app = webBuilder.Build();

    app.UseSerilogRequestLogging();

    app.MapGet("/health", () => Results.Ok(new
    {
        status = "ok",
        transport = "http"
    }));

    app.MapMcp("/mcp");

    await app.RunAsync();
}
catch (Exception exception)
{
    Log.Fatal(exception, "HTTP host terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
