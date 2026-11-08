using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Infrastructure.Roslyn.Navigation;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>Supplies MethodAnalysis query data from the loaded Roslyn workspace.</summary>
internal sealed class RoslynMethodAnalysisQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynMethodAnalysisQueryDataProvider> logger) : IMethodAnalysisQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<AnalyzeMethodResult> AnalyzeMethodAsync(
        AnalyzeMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MethodSignatureDescriptor? signature = null;

        try
        {
            var resolved = await helper.ResolveSymbolAsync(
                request.SolutionPath, request.FilePath, request.Line, request.Column, cancellationToken);
            if (resolved.FailureReason is not null)
            {
                return Failure(signature, resolved.FailureReason, resolved.Guidance);
            }

            var callable = resolved.Symbol is null ? null : AnalysisCallableHelper.ResolveCallableSymbol(resolved.Symbol);
            if (callable is null || resolved.Session is null)
            {
                return Failure(null,
                    "The resolved symbol does not have an analyzable callable body.",
                    "Use a source method, constructor, local function, property accessor, or event accessor.");
            }

            signature = CallableMappingHelper.MapMethodSignature(callable);
            var context = await AnalysisCallableHelper.ResolveDeclarationAsync(resolved.Session.Solution, callable, cancellationToken);
            if (context is null)
            {
                return Failure(signature,
                    "The resolved callable symbol does not have source syntax available.",
                    "Use a callable member declared in a source document of the loaded solution.");
            }

            var statements = GetCallableBodyStatements(context.Declaration);
            if (statements is null)
            {
                return Failure(signature,
                    "The resolved callable symbol does not contain a block body that supports flow analysis.",
                    "Use a callable member with a block body, or move the cursor onto a method or accessor implementation instead of an expression-bodied member.");
            }

            if (statements.Value.Count == 0)
            {
                return Failure(signature,
                    "The resolved callable symbol does not contain analyzable statements.",
                    "Use a callable member with a block body that contains one or more statements.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var firstStatement = statements.Value[0];
            var lastStatement = statements.Value[^1];
            var dataFlow = BuildDataFlowDescriptor(context.SemanticModel.AnalyzeDataFlow(firstStatement, lastStatement));
            var controlFlow = BuildControlFlowDescriptor(context.SemanticModel.AnalyzeControlFlow(firstStatement, lastStatement));
            var outgoingCalls = await AnalysisCallableHelper.CollectOutgoingCallsAsync(context, Math.Min(request.MaxOutgoingCalls, 200), cancellationToken);
            return new AnalyzeMethodResult(signature, dataFlow, controlFlow, outgoingCalls, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to analyze method for {FilePath}:{Line}:{Column}", request.FilePath, request.Line, request.Column);
            return Failure(signature,
                "The server could not analyze the resolved callable member.",
                "Retry the request on a source callable member inside the loaded solution.");
        }
    }

    private static AnalyzeMethodResult Failure(MethodSignatureDescriptor? signature, string reason, string? guidance) =>
        new(signature, null, null, Array.Empty<CallEdgeDescriptor>(), reason, guidance);

    private static SyntaxList<StatementSyntax>? GetCallableBodyStatements(SyntaxNode declaration) =>
        declaration switch
        {
            BaseMethodDeclarationSyntax method when method.Body is not null => method.Body.Statements,
            LocalFunctionStatementSyntax localFunction when localFunction.Body is not null => localFunction.Body.Statements,
            AccessorDeclarationSyntax accessor when accessor.Body is not null => accessor.Body.Statements,
            _ => null
        };

    private static DataFlowDescriptor BuildDataFlowDescriptor(DataFlowAnalysis analysis) =>
        new(
            VariablesDeclared: MapNames(analysis.VariablesDeclared),
            DataFlowsIn: MapNames(analysis.DataFlowsIn),
            DataFlowsOut: MapNames(analysis.DataFlowsOut),
            ReadInside: MapNames(analysis.ReadInside),
            WrittenInside: MapNames(analysis.WrittenInside),
            ReadOutside: MapNames(analysis.ReadOutside),
            WrittenOutside: MapNames(analysis.WrittenOutside),
            Captured: MapNames(analysis.Captured),
            AlwaysAssigned: MapNames(analysis.AlwaysAssigned),
            Succeeded: analysis.Succeeded);

    private static string[] MapNames(IEnumerable<ISymbol> symbols) =>
        symbols.Select(static symbol => symbol.Name).Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray();

    private static ControlFlowDescriptor BuildControlFlowDescriptor(ControlFlowAnalysis analysis) =>
        new(
            StartPointIsReachable: analysis.StartPointIsReachable,
            EndPointIsReachable: analysis.EndPointIsReachable,
            EntryPoints: MapLocations(analysis.EntryPoints),
            ExitPoints: MapLocations(analysis.ExitPoints),
            ReturnStatements: MapLocations(analysis.ReturnStatements),
            Succeeded: analysis.Succeeded);

    private static SymbolLocation[] MapLocations(IEnumerable<SyntaxNode> nodes) =>
        nodes.Select(MapSyntaxLocation).Where(static location => location is not null).Cast<SymbolLocation>().ToArray();

    private static SymbolLocation? MapSyntaxLocation(SyntaxNode node)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        return string.IsNullOrWhiteSpace(lineSpan.Path)
            ? null
            : new SymbolLocation(lineSpan.Path, lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1);
    }
}
