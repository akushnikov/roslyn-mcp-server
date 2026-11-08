using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>Resolves callable signatures using the shared Roslyn symbol and callable mapping helpers.</summary>
internal sealed class RoslynMethodSignatureQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynMethodSignatureQueryDataProvider> logger) : IMethodSignatureQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<GetMethodSignatureResult> GetMethodSignatureAsync(
        GetMethodSignatureRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var resolved = await helper.ResolveSymbolAsync(
                request.SolutionPath,
                request.FilePath,
                request.Line,
                request.Column,
                cancellationToken);

            if (resolved.FailureReason is not null)
            {
                return new GetMethodSignatureResult(null, resolved.FailureReason, resolved.Guidance);
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
                return new GetMethodSignatureResult(
                    Signature: null,
                    FailureReason: "The resolved symbol is not a callable member.",
                    Guidance: "Use a method, constructor, local function, accessor, property, or event declaration.");
            }

            return new GetMethodSignatureResult(CallableMappingHelper.MapMethodSignature(methodSymbol), null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to retrieve the method signature for {FilePath}:{Line}:{Column}",
                request.FilePath,
                request.Line,
                request.Column);

            return new GetMethodSignatureResult(
                Signature: null,
                FailureReason: "The server could not retrieve the method signature.",
                Guidance: "Retry the request and ensure the solution is loaded.");
        }
    }
}
