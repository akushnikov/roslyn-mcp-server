using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Navigation;

/// <summary>Projects attribute query outcomes while retaining symbol context and distinct cancellation.</summary>
public sealed class SymbolAttributesQueryService(SymbolAttributesQueryOperation operation) : ISymbolAttributesQueryService
{
    /// <inheritdoc />
    public async ValueTask<GetSymbolAttributesResult> GetSymbolAttributesAsync(
        GetSymbolAttributesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new GetSymbolAttributesResult(
                Symbol: error.Symbol,
                Attributes: Array.Empty<SymbolAttributeDescriptor>(),
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new GetSymbolAttributesResult(
                Symbol: null,
                Attributes: Array.Empty<SymbolAttributeDescriptor>(),
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
