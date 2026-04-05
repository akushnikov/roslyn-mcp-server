using Microsoft.Extensions.DependencyInjection;
using RoslynMcpServer.Application.DependencyInjection;
using RoslynMcpServer.Infrastructure.Roslyn.DependencyInjection;

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
        return services;
    }
}
