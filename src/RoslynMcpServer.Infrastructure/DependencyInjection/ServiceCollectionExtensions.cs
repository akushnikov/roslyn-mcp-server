using Microsoft.Extensions.DependencyInjection;
using RoslynMcpServer.Abstractions.Server.Services;
using RoslynMcpServer.Application.DependencyInjection;
using RoslynMcpServer.Application.Server;
using RoslynMcpServer.Infrastructure.Roslyn.DependencyInjection;
using RoslynMcpServer.Infrastructure.Server;

namespace RoslynMcpServer.Infrastructure.DependencyInjection;

/// <summary>
/// Registers the full Roslyn MCP server service graph shared by all hosts.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers application services together with Roslyn infrastructure services.
    /// </summary>
    public static IServiceCollection AddRoslynMcpServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRoslynMcpServerApplication();
        services.AddRoslynMcpServerRoslynInfrastructure();
        services.AddSingleton<IServerInfoQueryDataProvider>(_ => new ServerInfoQueryDataProvider(
            typeof(ServerInfoQueryService).Assembly.GetName().Version?.ToString() ?? "0.1.0"));
        return services;
    }
}
