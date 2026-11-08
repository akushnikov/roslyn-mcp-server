using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Infrastructure.Roslyn.Navigation;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>Supplies TypeCompatibility query data from the loaded Roslyn workspace.</summary>
internal sealed class RoslynTypeCompatibilityQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynTypeCompatibilityQueryDataProvider> logger) : ITypeCompatibilityQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<CheckTypeCompatibilityResult> CheckTypeCompatibilityAsync(
        CheckTypeCompatibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SymbolDescriptor? sourceDescriptor = null;
        SymbolDescriptor? targetDescriptor = null;

        try
        {
            var source = await helper.ResolveSymbolAsync(request.SolutionPath, request.SourceFilePath,
                request.SourceLine, request.SourceColumn, cancellationToken);
            if (source.FailureReason is not null)
            {
                return new(null, null, null, source.FailureReason, source.Guidance);
            }

            sourceDescriptor = SymbolResolutionHelper.MapSymbol(source.Symbol);
            var target = await helper.ResolveSymbolAsync(request.SolutionPath, request.TargetFilePath,
                request.TargetLine, request.TargetColumn, cancellationToken);
            if (target.FailureReason is not null)
            {
                return new(sourceDescriptor, null, null, target.FailureReason, target.Guidance);
            }

            targetDescriptor = SymbolResolutionHelper.MapSymbol(target.Symbol);
            var sourceType = GetTypeSymbol(source.Symbol);
            var targetType = GetTypeSymbol(target.Symbol);
            if (sourceType is null || targetType is null || source.Document is null || target.Document is null)
            {
                return new(sourceDescriptor, targetDescriptor, null,
                    "The requested symbols do not both resolve to analyzable types.",
                    "Use type declarations, typed members, parameters, locals, or fields.");
            }

            // Resolve both symbols into one compilation, including referenced-project types.
            // Never classify unrelated compilation symbols or substitute types by display name.
            foreach (var project in new[] { source.Document.Project, target.Document.Project }.DistinctBy(project => project.Id))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await project.GetCompilationAsync(cancellationToken) is not CSharpCompilation compilation)
                {
                    continue;
                }

                var compatibleSource = SymbolFinder.FindSimilarSymbols(sourceType, compilation, cancellationToken).Take(2).ToArray();
                var compatibleTarget = SymbolFinder.FindSimilarSymbols(targetType, compilation, cancellationToken).Take(2).ToArray();
                if (compatibleSource.Length != 1 || compatibleTarget.Length != 1)
                {
                    continue;
                }

                var conversion = compilation.ClassifyConversion(compatibleSource[0], compatibleTarget[0]);
                cancellationToken.ThrowIfCancellationRequested();
                return new(sourceDescriptor, targetDescriptor,
                    new TypeCompatibilityDescriptor(sourceType.ToDisplayString(), targetType.ToDisplayString(),
                        conversion.IsIdentity, conversion.Exists, conversion.IsImplicit, conversion.IsExplicit,
                        conversion.IsReference, conversion.IsBoxing, conversion.IsNumeric, conversion.IsNullable),
                    null, null);
            }

            return new(sourceDescriptor, targetDescriptor, null,
                "The requested types could not be resolved in a shared C# compilation context.",
                "Use types available together in a loaded C# project or its project references.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to check type compatibility for {SourceFilePath} and {TargetFilePath}",
                request.SourceFilePath, request.TargetFilePath);
            return new(sourceDescriptor, targetDescriptor, null,
                "The server could not check type compatibility for the requested symbols.",
                "Retry the request on typed symbols inside the loaded solution.");
        }
    }

    private static ITypeSymbol? GetTypeSymbol(ISymbol? symbol) => symbol switch
    {
        ITypeSymbol type => type,
        ILocalSymbol local => local.Type,
        IParameterSymbol parameter => parameter.Type,
        IFieldSymbol field => field.Type,
        IPropertySymbol property => property.Type,
        IMethodSymbol method => method.ReturnType,
        IEventSymbol eventSymbol => eventSymbol.Type,
        _ => null
    };
}
