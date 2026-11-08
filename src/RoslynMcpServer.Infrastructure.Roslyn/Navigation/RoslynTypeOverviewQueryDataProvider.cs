using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>Retrieves a type's direct relationships and explicit members from a loaded Roslyn workspace.</summary>
internal sealed class RoslynTypeOverviewQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynTypeOverviewQueryDataProvider> logger) : ITypeOverviewQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<GetTypeOverviewResult> GetTypeOverviewAsync(
        GetTypeOverviewRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var resolved = await helper.ResolveSymbolAsync(
                request.SolutionPath, request.FilePath, request.Line, request.Column, cancellationToken);
            if (resolved.FailureReason is not null)
            {
                return new GetTypeOverviewResult(null, null, Array.Empty<SymbolDescriptor>(),
                    Array.Empty<TypeMemberDescriptor>(), resolved.FailureReason, resolved.Guidance);
            }

            var typeSymbol = resolved.Symbol switch
            {
                INamedTypeSymbol namedType => namedType,
                ISymbol { ContainingType: not null } symbol => symbol.ContainingType,
                _ => null
            };

            if (typeSymbol is null)
            {
                return new GetTypeOverviewResult(
                    Type: SymbolResolutionHelper.MapSymbol(resolved.Symbol),
                    BaseType: null,
                    Interfaces: Array.Empty<SymbolDescriptor>(),
                    Members: Array.Empty<TypeMemberDescriptor>(),
                    FailureReason: "The resolved symbol does not belong to a named type.",
                    Guidance: "Use a type declaration or a member inside a named type.");
            }

            var members = typeSymbol.GetMembers()
                .Where(static member => !member.IsImplicitlyDeclared)
                .Select(TypeMemberMappingHelper.MapTypeMember)
                .Where(static member => member is not null)
                .Cast<TypeMemberDescriptor>()
                .OrderBy(static member => member.Kind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static member => member.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var interfaces = typeSymbol.Interfaces
                .Select(SymbolResolutionHelper.MapSymbol)
                .Where(static symbol => symbol is not null)
                .Cast<SymbolDescriptor>()
                .ToArray();

            return new GetTypeOverviewResult(
                Type: SymbolResolutionHelper.MapSymbol(typeSymbol),
                BaseType: SymbolResolutionHelper.MapSymbol(typeSymbol.BaseType),
                Interfaces: interfaces,
                Members: members,
                FailureReason: null,
                Guidance: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to retrieve type overview for {FilePath}:{Line}:{Column}",
                request.FilePath, request.Line, request.Column);
            return new GetTypeOverviewResult(null, null, Array.Empty<SymbolDescriptor>(),
                Array.Empty<TypeMemberDescriptor>(), "The server could not retrieve the type overview.",
                "Retry the request and ensure the solution is loaded.");
        }
    }
}
