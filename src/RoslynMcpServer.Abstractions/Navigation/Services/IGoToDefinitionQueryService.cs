using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>
/// Defines the application boundary for the go-to-definition query used by MCP adapters.
/// </summary>
public interface IGoToDefinitionQueryService
{
    /// <summary>
    /// Resolves source definition locations for the symbol at a source position.
    /// </summary>
    ValueTask<GoToDefinitionResult> GoToDefinitionAsync(
        GoToDefinitionRequest request,
        CancellationToken cancellationToken = default);
}
