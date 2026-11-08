using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Infrastructure.Roslyn.Navigation;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>Shares callable selection and declaration-owned outgoing-call analysis.</summary>
internal static class AnalysisCallableHelper
{
    internal static IMethodSymbol? ResolveCallableSymbol(ISymbol symbol) =>
        symbol switch
        {
            IMethodSymbol method => method,
            IPropertySymbol property => CallableMappingHelper.CreatePropertyAccessorSignature(property),
            IEventSymbol eventSymbol => CallableMappingHelper.CreateEventSignature(eventSymbol),
            _ => null
        };

    /// <summary>Resolves syntax and its semantic model from the solution that owns the callable.</summary>
    internal static async ValueTask<CallableDeclarationContext?> ResolveDeclarationAsync(
        Solution solution,
        IMethodSymbol callable,
        CancellationToken cancellationToken)
    {
        foreach (var syntaxReference in callable.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var document = solution.GetDocument(syntaxReference.SyntaxTree);
            if (document is null)
            {
                continue;
            }

            var declaration = await syntaxReference.GetSyntaxAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (semanticModel is not null && semanticModel.SyntaxTree == declaration.SyntaxTree)
            {
                return new CallableDeclarationContext(document, declaration, semanticModel, callable);
            }
        }

        return null;
    }

    /// <summary>Collects invocations before object creations, preserving legacy ordering and result caps.</summary>
    internal static ValueTask<IReadOnlyList<CallEdgeDescriptor>> CollectOutgoingCallsAsync(
        CallableDeclarationContext context,
        int maxResults,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var calls = new List<CallEdgeDescriptor>();
        foreach (var invocation in context.Declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = context.SemanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
            var location = MapSyntaxLocation(invocation);
            if (symbol is null || location is null)
            {
                continue;
            }

            calls.Add(new CallEdgeDescriptor(SymbolResolutionHelper.MapSymbol(symbol), location, invocation.ToString()));
        }

        foreach (var creation in context.Declaration.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = context.SemanticModel.GetSymbolInfo(creation, cancellationToken).Symbol as IMethodSymbol;
            var location = MapSyntaxLocation(creation);
            if (symbol is null || location is null)
            {
                continue;
            }

            calls.Add(new CallEdgeDescriptor(SymbolResolutionHelper.MapSymbol(symbol), location, creation.ToString()));
        }

        IReadOnlyList<CallEdgeDescriptor> results = calls
            .DistinctBy(static call => $"{call.Location.FilePath}:{call.Location.Line}:{call.Location.Column}:{call.Target?.DisplayName}")
            .Take(Math.Min(maxResults, 200))
            .ToArray();
        return ValueTask.FromResult(results);
    }

    private static SymbolLocation? MapSyntaxLocation(SyntaxNode node)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        return string.IsNullOrWhiteSpace(lineSpan.Path)
            ? null
            : new SymbolLocation(
                lineSpan.Path,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1);
    }

    /// <summary>Keeps declaration syntax paired with the semantic model for the same tree.</summary>
    internal sealed record CallableDeclarationContext(
        Document Document,
        SyntaxNode Declaration,
        SemanticModel SemanticModel,
        IMethodSymbol Callable);
}
