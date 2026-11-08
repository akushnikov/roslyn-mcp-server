using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>Retrieves ordered source declarations and their locations from a loaded Roslyn document.</summary>
internal sealed class RoslynDocumentOutlineQueryDataProvider(
    IWorkspaceSessionProvider workspaceSessionProvider) : IDocumentOutlineQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<GetDocumentOutlineResult> GetDocumentOutlineAsync(
        GetDocumentOutlineRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var session = await workspaceSessionProvider.GetReadySessionAsync(request.SolutionPath, cancellationToken);
        if (session is null)
        {
            return new GetDocumentOutlineResult(
                FilePath: null,
                Nodes: Array.Empty<DocumentOutlineNode>(),
                FailureReason: "The requested solution is not currently loaded.",
                Guidance: "Call load_solution first for the requested solutionPath.");
        }

        var document = SymbolResolutionHelper.FindDocument(session, request.FilePath);
        if (document is null)
        {
            return new GetDocumentOutlineResult(
                FilePath: SymbolResolutionHelper.NormalizePath(request.FilePath),
                Nodes: Array.Empty<DocumentOutlineNode>(),
                FailureReason: "The requested file was not found in the loaded solution.",
                Guidance: "Call get_project_structure with includeDocuments=true to inspect valid file paths.");
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return new GetDocumentOutlineResult(
                FilePath: document.FilePath,
                Nodes: Array.Empty<DocumentOutlineNode>(),
                FailureReason: "The requested document does not have a C# compilation unit syntax tree.",
                Guidance: "Use a C# source file that belongs to the loaded solution.");
        }

        return new GetDocumentOutlineResult(
            FilePath: document.FilePath,
            Nodes: BuildOutline(compilationUnit.Members),
            FailureReason: null,
            Guidance: null);
    }

    private static IReadOnlyList<DocumentOutlineNode> BuildOutline(SyntaxList<MemberDeclarationSyntax> members)
    {
        var result = new List<DocumentOutlineNode>();

        foreach (var member in members)
        {
            switch (member)
            {
                case NamespaceDeclarationSyntax namespaceDeclaration:
                    result.Add(CreateNode(
                        namespaceDeclaration.Name.ToString(),
                        "Namespace",
                        namespaceDeclaration,
                        BuildOutline(namespaceDeclaration.Members)));
                    break;
                case FileScopedNamespaceDeclarationSyntax fileScopedNamespace:
                    result.Add(CreateNode(
                        fileScopedNamespace.Name.ToString(),
                        "Namespace",
                        fileScopedNamespace,
                        BuildOutline(fileScopedNamespace.Members)));
                    break;
                case BaseTypeDeclarationSyntax baseType:
                    result.Add(CreateNode(
                        baseType.Identifier.Text,
                        GetBaseTypeKind(baseType),
                        baseType,
                        BuildTypeChildren(baseType)));
                    break;
                case DelegateDeclarationSyntax delegateDeclaration:
                    result.Add(CreateNode(
                        delegateDeclaration.Identifier.Text,
                        "Delegate",
                        delegateDeclaration,
                        Array.Empty<DocumentOutlineNode>()));
                    break;
            }
        }

        return result;
    }

    private static IReadOnlyList<DocumentOutlineNode> BuildTypeChildren(BaseTypeDeclarationSyntax typeDeclaration)
    {
        if (typeDeclaration is EnumDeclarationSyntax enumDeclaration)
        {
            return enumDeclaration.Members
                .Select(memberDeclaration => CreateNode(
                    memberDeclaration.Identifier.Text,
                    "EnumMember",
                    memberDeclaration,
                    Array.Empty<DocumentOutlineNode>()))
                .ToArray();
        }

        if (typeDeclaration is not TypeDeclarationSyntax typeSyntax)
        {
            return Array.Empty<DocumentOutlineNode>();
        }

        var children = new List<DocumentOutlineNode>();
        foreach (var member in typeSyntax.Members)
        {
            switch (member)
            {
                case BaseTypeDeclarationSyntax nestedType:
                    children.Add(CreateNode(
                        nestedType.Identifier.Text,
                        GetBaseTypeKind(nestedType),
                        nestedType,
                        BuildTypeChildren(nestedType)));
                    break;
                case MethodDeclarationSyntax method:
                    children.Add(CreateNode(method.Identifier.Text, "Method", method, Array.Empty<DocumentOutlineNode>()));
                    break;
                case ConstructorDeclarationSyntax constructor:
                    children.Add(CreateNode(constructor.Identifier.Text, "Constructor", constructor, Array.Empty<DocumentOutlineNode>()));
                    break;
                case DestructorDeclarationSyntax destructor:
                    children.Add(CreateNode($"~{destructor.Identifier.Text}", "Destructor", destructor, Array.Empty<DocumentOutlineNode>()));
                    break;
                case PropertyDeclarationSyntax property:
                    children.Add(CreateNode(property.Identifier.Text, "Property", property, Array.Empty<DocumentOutlineNode>()));
                    break;
                case IndexerDeclarationSyntax indexer:
                    children.Add(CreateNode("this[]", "Indexer", indexer, Array.Empty<DocumentOutlineNode>()));
                    break;
                case EventDeclarationSyntax eventDeclaration:
                    children.Add(CreateNode(eventDeclaration.Identifier.Text, "Event", eventDeclaration, Array.Empty<DocumentOutlineNode>()));
                    break;
                case EventFieldDeclarationSyntax eventField:
                    children.AddRange(eventField.Declaration.Variables.Select(variable => CreateNode(
                        variable.Identifier.Text,
                        "Event",
                        variable,
                        Array.Empty<DocumentOutlineNode>())));
                    break;
                case FieldDeclarationSyntax field:
                    var fieldKind = field.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.ConstKeyword))
                        ? "Constant"
                        : "Field";
                    children.AddRange(field.Declaration.Variables.Select(variable => CreateNode(
                        variable.Identifier.Text,
                        fieldKind,
                        variable,
                        Array.Empty<DocumentOutlineNode>())));
                    break;
                case DelegateDeclarationSyntax delegateDeclaration:
                    children.Add(CreateNode(delegateDeclaration.Identifier.Text, "Delegate", delegateDeclaration, Array.Empty<DocumentOutlineNode>()));
                    break;
            }
        }

        return children;
    }

    private static DocumentOutlineNode CreateNode(
        string name,
        string kind,
        SyntaxNode node,
        IReadOnlyList<DocumentOutlineNode> children)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        return new DocumentOutlineNode(
            Name: name,
            Kind: kind,
            Start: new SymbolLocation(
                node.SyntaxTree.FilePath,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1),
            End: new SymbolLocation(
                node.SyntaxTree.FilePath,
                lineSpan.EndLinePosition.Line + 1,
                lineSpan.EndLinePosition.Character + 1),
            Children: children);
    }

    private static string GetBaseTypeKind(BaseTypeDeclarationSyntax declaration) =>
        declaration switch
        {
            ClassDeclarationSyntax => "Class",
            StructDeclarationSyntax => "Struct",
            InterfaceDeclarationSyntax => "Interface",
            EnumDeclarationSyntax => "Enum",
            RecordDeclarationSyntax => "Record",
            _ => declaration.Kind().ToString()
        };
}
