using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Application.Analysis.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Analysis;

/// <summary>
/// Projects type hierarchy query outcomes to the public result without storing request context.
/// </summary>
public sealed class TypeHierarchyQueryService(
    TypeHierarchyQueryOperation operation) : ITypeHierarchyQueryService
{
    /// <inheritdoc />
    public async ValueTask<GetTypeHierarchyResult> GetTypeHierarchyAsync(
        GetTypeHierarchyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new GetTypeHierarchyResult(
                Symbol: error.Symbol,
                BaseTypes: Array.Empty<SymbolDescriptor>(),
                DerivedTypes: Array.Empty<SymbolDescriptor>(),
                Interfaces: Array.Empty<SymbolDescriptor>(),
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new GetTypeHierarchyResult(
                Symbol: null,
                BaseTypes: Array.Empty<SymbolDescriptor>(),
                DerivedTypes: Array.Empty<SymbolDescriptor>(),
                Interfaces: Array.Empty<SymbolDescriptor>(),
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
