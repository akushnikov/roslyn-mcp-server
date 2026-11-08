using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Infrastructure.Roslyn.Navigation;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>Supplies OutgoingCalls query data from the loaded Roslyn workspace.</summary>
internal sealed class RoslynOutgoingCallsQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynOutgoingCallsQueryDataProvider> logger) : IOutgoingCallsQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<GetOutgoingCallsResult> GetOutgoingCallsAsync(
        GetOutgoingCallsRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SymbolDescriptor? resolvedSymbol = null;

        try
        {
            var resolved = await helper.ResolveSymbolAsync(
                request.SolutionPath, request.FilePath, request.Line, request.Column, cancellationToken);
            if (resolved.FailureReason is not null)
            {
                return new GetOutgoingCallsResult(null, Array.Empty<CallEdgeDescriptor>(), resolved.FailureReason, resolved.Guidance);
            }

            resolvedSymbol = SymbolResolutionHelper.MapSymbol(resolved.Symbol);
            var callable = resolved.Symbol is null ? null : AnalysisCallableHelper.ResolveCallableSymbol(resolved.Symbol);
            if (callable is null || resolved.Document is null || resolved.Session is null)
            {
                return new GetOutgoingCallsResult(
                    resolvedSymbol, Array.Empty<CallEdgeDescriptor>(),
                    "The resolved symbol does not have an analyzable callable body.",
                    "Use a source method, constructor, local function, property accessor, or event accessor.");
            }

            resolvedSymbol = SymbolResolutionHelper.MapSymbol(callable);
            if (callable.DeclaringSyntaxReferences.Length == 0)
            {
                return new GetOutgoingCallsResult(
                    resolvedSymbol, Array.Empty<CallEdgeDescriptor>(),
                    "The resolved callable symbol does not have source syntax available.",
                    "Use a callable member declared in a source document of the loaded solution.");
            }

            var declaration = await AnalysisCallableHelper.ResolveDeclarationAsync(
                resolved.Session.Solution, callable, cancellationToken);
            if (declaration is null)
            {
                return new GetOutgoingCallsResult(
                    resolvedSymbol, Array.Empty<CallEdgeDescriptor>(),
                    "The requested document does not have semantic information available.",
                    "Use a source document that belongs to a loaded C# project.");
            }

            var calls = await AnalysisCallableHelper.CollectOutgoingCallsAsync(declaration, request.MaxResults, cancellationToken);
            return new GetOutgoingCallsResult(resolvedSymbol, calls, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to compute outgoing calls for {FilePath}:{Line}:{Column}", request.FilePath, request.Line, request.Column);
            return new GetOutgoingCallsResult(
                resolvedSymbol, Array.Empty<CallEdgeDescriptor>(),
                "The server could not compute outgoing calls for the resolved symbol.",
                "Retry the request on a callable symbol inside the loaded solution.");
        }
    }
}
