using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>Resolves callable source from the declaration's syntax tree, including cross-document references.</summary>
internal sealed class RoslynMethodSourceQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynMethodSourceQueryDataProvider> logger) : IMethodSourceQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<GetMethodSourceResult> GetMethodSourceAsync(
        GetMethodSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var resolved = await helper.ResolveSymbolAsync(
                request.SolutionPath, request.FilePath, request.Line, request.Column, cancellationToken);
            if (resolved.FailureReason is not null)
            {
                return new GetMethodSourceResult(null, null, resolved.FailureReason, resolved.Guidance);
            }

            var methodSymbol = resolved.Symbol switch
            {
                IMethodSymbol method => method,
                IPropertySymbol property => CallableMappingHelper.CreatePropertyAccessorSignature(property),
                IEventSymbol eventSymbol => CallableMappingHelper.CreateEventSignature(eventSymbol),
                _ => null
            };
            if (methodSymbol is null)
            {
                return new GetMethodSourceResult(null, null,
                    "The resolved symbol is not a callable member.",
                    "Use a method, constructor, local function, accessor, property, or event declaration.");
            }

            var signature = CallableMappingHelper.MapMethodSignature(methodSymbol);
            var declarationNode = await GetCallableDeclarationNodeAsync(methodSymbol, cancellationToken);
            if (declarationNode is null)
            {
                return new GetMethodSourceResult(signature, null,
                    "The resolved callable member does not have source syntax available.",
                    "Use a callable member declared in a source document of the loaded solution.");
            }

            // A reference can resolve into a different document; its declaration owns both span and text.
            var sourceText = await declarationNode.SyntaxTree.GetTextAsync(cancellationToken);
            var span = declarationNode.GetLocation().GetLineSpan();
            var declarationPath = declarationNode.SyntaxTree.FilePath;

            return new GetMethodSourceResult(
                Signature: signature,
                Method: new MethodSourceDescriptor(
                    FilePath: declarationPath,
                    Start: new SymbolLocation(span.Path, span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1),
                    End: new SymbolLocation(span.Path, span.EndLinePosition.Line + 1, span.EndLinePosition.Character + 1),
                    Source: sourceText.ToString(declarationNode.Span)),
                FailureReason: null,
                Guidance: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to retrieve method source for {FilePath}:{Line}:{Column}",
                request.FilePath, request.Line, request.Column);
            return new GetMethodSourceResult(null, null,
                "The server could not retrieve method source.",
                "Retry the request and ensure the solution is loaded.");
        }
    }

    private static async ValueTask<SyntaxNode?> GetCallableDeclarationNodeAsync(
        IMethodSymbol methodSymbol,
        CancellationToken cancellationToken)
    {
        foreach (var syntaxReference in methodSymbol.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await syntaxReference.GetSyntaxAsync(cancellationToken) is SyntaxNode syntaxNode)
            {
                return syntaxNode;
            }
        }

        return null;
    }
}
