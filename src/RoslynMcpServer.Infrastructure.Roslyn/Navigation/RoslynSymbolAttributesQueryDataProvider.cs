using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>Resolves symbols and maps their attributes from a loaded Roslyn workspace.</summary>
internal sealed class RoslynSymbolAttributesQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynSymbolAttributesQueryDataProvider> logger) : ISymbolAttributesQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<GetSymbolAttributesResult> GetSymbolAttributesAsync(
        GetSymbolAttributesRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SymbolDescriptor? symbol = null;

        try
        {
            var resolved = await helper.ResolveSymbolAsync(
                request.SolutionPath,
                request.FilePath,
                request.Line,
                request.Column,
                cancellationToken);

            symbol = SymbolResolutionHelper.MapSymbol(resolved.Symbol);
            if (resolved.FailureReason is not null)
            {
                return new GetSymbolAttributesResult(
                    symbol, Array.Empty<SymbolAttributeDescriptor>(), resolved.FailureReason, resolved.Guidance);
            }

            var attributes = resolved.Symbol?
                .GetAttributes()
                .Select(MapAttribute)
                .OrderBy(static attribute => attribute.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<SymbolAttributeDescriptor>();

            cancellationToken.ThrowIfCancellationRequested();
            return new GetSymbolAttributesResult(symbol, attributes, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to retrieve symbol attributes for {FilePath}:{Line}:{Column}",
                request.FilePath,
                request.Line,
                request.Column);

            return new GetSymbolAttributesResult(
                Symbol: symbol,
                Attributes: Array.Empty<SymbolAttributeDescriptor>(),
                FailureReason: "The server could not retrieve symbol attributes.",
                Guidance: "Retry the request and ensure the solution is loaded.");
        }
    }

    private static SymbolAttributeDescriptor MapAttribute(AttributeData attribute) =>
        new(
            Name: attribute.AttributeClass?.Name ?? attribute.ToString() ?? "Attribute",
            DisplayName: attribute.AttributeClass?.ToDisplayString() ?? attribute.ToString() ?? "Attribute",
            ConstructorArguments: attribute.ConstructorArguments.Select(FormatTypedConstant).ToArray(),
            NamedArguments: attribute.NamedArguments.Select(static argument => $"{argument.Key} = {FormatTypedConstant(argument.Value)}").ToArray());

    private static string FormatTypedConstant(TypedConstant constant) =>
        constant.IsNull
            ? "null"
            : constant.Kind == TypedConstantKind.Array
                ? $"[{string.Join(", ", constant.Values.Select(FormatTypedConstant))}]"
                : constant.Value?.ToString() ?? constant.ToString() ?? string.Empty;
}
