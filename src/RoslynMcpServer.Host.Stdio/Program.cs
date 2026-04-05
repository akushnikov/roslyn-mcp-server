using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Infrastructure.DependencyInjection;
using RoslynMcpServer.Mcp;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddRoslynMcpServer();
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithRoslynMcpServer();

await builder.Build().RunAsync();
