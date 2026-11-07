using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Navigation;

/// <summary>
/// Projects go-to-definition pipeline outcomes into the public query result.
/// </summary>
public sealed class GoToDefinitionQueryService(
    GoToDefinitionQueryOperation operation) : IGoToDefinitionQueryService
{
    /// <inheritdoc />
    public async ValueTask<GoToDefinitionResult> GoToDefinitionAsync(
        GoToDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new GoToDefinitionResult(
                Symbol: null,
                Definitions: Array.Empty<SymbolLocation>(),
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new GoToDefinitionResult(
                Symbol: null,
                Definitions: Array.Empty<SymbolLocation>(),
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
