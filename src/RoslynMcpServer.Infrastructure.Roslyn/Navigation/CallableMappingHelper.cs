using Microsoft.CodeAnalysis;
using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>Preserves callable/accessor signature mapping shared by navigation providers.</summary>
internal static class CallableMappingHelper
{
    internal static MethodSignatureDescriptor MapMethodSignature(IMethodSymbol methodSymbol)
    {
        var kind = methodSymbol.MethodKind switch
        {
            MethodKind.Constructor or MethodKind.StaticConstructor => "Constructor",
            MethodKind.Destructor => "Destructor",
            MethodKind.LocalFunction => "LocalFunction",
            MethodKind.PropertyGet => "PropertyGet",
            MethodKind.PropertySet => "PropertySet",
            MethodKind.EventAdd => "EventAdd",
            MethodKind.EventRemove => "EventRemove",
            MethodKind.UserDefinedOperator => "Operator",
            MethodKind.Conversion => "Conversion",
            _ => "Method"
        };

        return new MethodSignatureDescriptor(
            Name: methodSymbol.Name,
            Kind: kind,
            DisplayName: methodSymbol.ToDisplayString(),
            Accessibility: methodSymbol.DeclaredAccessibility.ToString(),
            IsStatic: methodSymbol.IsStatic,
            IsAsync: methodSymbol.IsAsync,
            ReturnType: methodSymbol.MethodKind is MethodKind.Constructor or MethodKind.StaticConstructor or MethodKind.Destructor
                ? null
                : methodSymbol.ReturnType.ToDisplayString(),
            ContainingType: methodSymbol.ContainingType?.ToDisplayString(),
            Parameters: methodSymbol.Parameters.Select(MapParameter).ToArray(),
            Definition: SymbolResolutionHelper.GetSourceLocations(methodSymbol).FirstOrDefault());
    }

    internal static MethodParameterDescriptor MapParameter(IParameterSymbol parameter) =>
        new(
            Name: parameter.Name,
            Type: parameter.Type.ToDisplayString(),
            RefKind: parameter.RefKind.ToString(),
            IsOptional: parameter.IsOptional,
            DefaultValue: parameter.IsOptional && parameter.HasExplicitDefaultValue
                ? parameter.ExplicitDefaultValue?.ToString() ?? "null"
                : null);

    internal static IMethodSymbol? CreatePropertyAccessorSignature(IPropertySymbol propertySymbol) =>
        propertySymbol.GetMethod ?? propertySymbol.SetMethod;

    internal static IMethodSymbol? CreateEventSignature(IEventSymbol eventSymbol) =>
        eventSymbol.AddMethod ?? eventSymbol.RemoveMethod;
}
