using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Infrastructure.Roslyn.Navigation;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>
/// Supplies type hierarchy from Roslyn workspace sessions.
/// </summary>
internal sealed class RoslynTypeHierarchyQueryDataProvider(
    SymbolResolutionHelper helper) : ITypeHierarchyQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<GetTypeHierarchyResult> GetTypeHierarchyAsync(
        GetTypeHierarchyRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resolved = await helper.ResolveSymbolAsync(
            request.SolutionPath, request.FilePath, request.Line, request.Column, cancellationToken);
        if (resolved.FailureReason is not null)
        {
            return new GetTypeHierarchyResult(null, Array.Empty<SymbolDescriptor>(), Array.Empty<SymbolDescriptor>(), Array.Empty<SymbolDescriptor>(), resolved.FailureReason, resolved.Guidance);
        }

        var typeSymbol = resolved.Symbol switch
        {
            INamedTypeSymbol namedType => namedType,
            ISymbol { ContainingType: not null } symbol => symbol.ContainingType,
            _ => null
        };

        if (typeSymbol is null)
        {
            return new GetTypeHierarchyResult(
                Symbol: SymbolResolutionHelper.MapSymbol(resolved.Symbol),
                BaseTypes: Array.Empty<SymbolDescriptor>(),
                DerivedTypes: Array.Empty<SymbolDescriptor>(),
                Interfaces: Array.Empty<SymbolDescriptor>(),
                FailureReason: "The resolved symbol does not have a type hierarchy.",
                Guidance: "Use a type declaration or a member that belongs to a named type.");
        }

        var direction = request.Direction?.Trim();
        var includeAncestors = string.IsNullOrWhiteSpace(direction) || direction.Equals("Both", StringComparison.OrdinalIgnoreCase) || direction.Equals("Ancestors", StringComparison.OrdinalIgnoreCase);
        var includeDescendants = string.IsNullOrWhiteSpace(direction) || direction.Equals("Both", StringComparison.OrdinalIgnoreCase) || direction.Equals("Descendants", StringComparison.OrdinalIgnoreCase);

        var baseTypes = includeAncestors
            ? EnumerateBaseTypes(typeSymbol).Select(SymbolResolutionHelper.MapSymbol).Where(static descriptor => descriptor is not null).Cast<SymbolDescriptor>().ToArray()
            : Array.Empty<SymbolDescriptor>();

        var derivedTypes = includeDescendants
            ? await FindDerivedTypesAsync(typeSymbol, resolved.Session?.Solution, cancellationToken)
            : Array.Empty<SymbolDescriptor>();

        var interfaces = includeAncestors
            ? typeSymbol.AllInterfaces.Select(SymbolResolutionHelper.MapSymbol).Where(static descriptor => descriptor is not null).Cast<SymbolDescriptor>().ToArray()
            : Array.Empty<SymbolDescriptor>();

        return new GetTypeHierarchyResult(SymbolResolutionHelper.MapSymbol(typeSymbol), baseTypes, derivedTypes, interfaces, null, null);
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateBaseTypes(INamedTypeSymbol typeSymbol)
    {
        for (var current = typeSymbol.BaseType; current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }

    private static async Task<IReadOnlyList<SymbolDescriptor>> FindDerivedTypesAsync(
        INamedTypeSymbol typeSymbol,
        Solution? solution,
        CancellationToken cancellationToken)
    {
        if (solution is null)
        {
            return Array.Empty<SymbolDescriptor>();
        }

        IEnumerable<INamedTypeSymbol> derivedTypes = typeSymbol.TypeKind switch
        {
            TypeKind.Interface => await SymbolFinder.FindImplementationsAsync(typeSymbol, solution, cancellationToken: cancellationToken),
            _ => await SymbolFinder.FindDerivedClassesAsync(typeSymbol, solution, transitive: true, cancellationToken: cancellationToken)
        };

        return derivedTypes
            .Select(SymbolResolutionHelper.MapSymbol)
            .Where(static descriptor => descriptor is not null)
            .Cast<SymbolDescriptor>()
            .DistinctBy(static descriptor => $"{descriptor.Definition?.FilePath}:{descriptor.Definition?.Line}:{descriptor.Definition?.Column}:{descriptor.DisplayName}")
            .ToArray();
    }
}
