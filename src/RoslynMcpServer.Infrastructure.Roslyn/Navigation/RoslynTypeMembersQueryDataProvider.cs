using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>Retrieves explicit type members from a loaded Roslyn workspace.</summary>
internal sealed class RoslynTypeMembersQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynTypeMembersQueryDataProvider> logger) : ITypeMembersQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<GetTypeMembersResult> GetTypeMembersAsync(
        GetTypeMembersRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var resolved = await helper.ResolveSymbolAsync(
                request.SolutionPath, request.FilePath, request.Line, request.Column, cancellationToken);
            if (resolved.FailureReason is not null)
            {
                return new GetTypeMembersResult(null, Array.Empty<TypeMemberDescriptor>(), resolved.FailureReason, resolved.Guidance);
            }

            var typeSymbol = resolved.Symbol switch
            {
                INamedTypeSymbol namedType => namedType,
                ISymbol { ContainingType: not null } symbol => symbol.ContainingType,
                _ => null
            };

            if (typeSymbol is null)
            {
                return new GetTypeMembersResult(
                    Type: SymbolResolutionHelper.MapSymbol(resolved.Symbol),
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

            return new GetTypeMembersResult(SymbolResolutionHelper.MapSymbol(typeSymbol), members, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to retrieve type members for {FilePath}:{Line}:{Column}", request.FilePath, request.Line, request.Column);
            return new GetTypeMembersResult(null, Array.Empty<TypeMemberDescriptor>(),
                "The server could not retrieve type members.", "Retry the request and ensure the solution is loaded.");
        }
    }
}
