using Microsoft.Extensions.DependencyInjection;

namespace RoslynMcpServer.Mcp;

public static class McpServerRegistrationExtensions
{
    public static IMcpServerBuilder WithRoslynMcpServer(this IMcpServerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithToolsFromAssembly(typeof(McpServerRegistrationExtensions).Assembly);
    }
}
