using Microsoft.CodeAnalysis;
using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>Preserves member descriptor mapping shared by type navigation providers.</summary>
internal static class TypeMemberMappingHelper
{
    internal static TypeMemberDescriptor? MapTypeMember(ISymbol member)
    {
        var descriptor = SymbolResolutionHelper.MapSymbol(member);
        if (descriptor is null)
        {
            return null;
        }

        return new TypeMemberDescriptor(
            Name: member.Name,
            Kind: descriptor.Kind,
            DisplayName: member.ToDisplayString(),
            Accessibility: member.DeclaredAccessibility.ToString(),
            IsStatic: member.IsStatic,
            Definition: SymbolResolutionHelper.GetSourceLocations(member).FirstOrDefault());
    }
}
