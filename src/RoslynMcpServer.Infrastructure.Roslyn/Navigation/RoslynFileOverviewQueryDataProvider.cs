using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>Reads namespace and type summaries from a C# document in a loaded Roslyn workspace.</summary>
internal sealed class RoslynFileOverviewQueryDataProvider(
    IWorkspaceSessionProvider workspaceSessionProvider,
    ILogger<RoslynFileOverviewQueryDataProvider> logger) : IFileOverviewQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<GetFileOverviewResult> GetFileOverviewAsync(
        GetFileOverviewRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var session = await workspaceSessionProvider.GetReadySessionAsync(request.SolutionPath, cancellationToken);
            if (session is null)
            {
                return new GetFileOverviewResult(null, Array.Empty<string>(), Array.Empty<FileTypeDescriptor>(),
                    "The requested solution is not currently loaded.", "Call load_solution first for the requested solutionPath.");
            }

            var document = SymbolResolutionHelper.FindDocument(session, request.FilePath);
            if (document is null)
            {
                return new GetFileOverviewResult(SymbolResolutionHelper.NormalizePath(request.FilePath), Array.Empty<string>(), Array.Empty<FileTypeDescriptor>(),
                    "The requested file was not found in the loaded solution.", "Call get_project_structure with includeDocuments=true to inspect valid file paths.");
            }

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root is not CompilationUnitSyntax compilationUnit)
            {
                return new GetFileOverviewResult(document.FilePath, Array.Empty<string>(), Array.Empty<FileTypeDescriptor>(),
                    "The requested document does not have a C# compilation unit syntax tree.", "Use a C# source file that belongs to the loaded solution.");
            }

            var namespaces = compilationUnit.Members
                .SelectMany(GetNamespaces)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var types = compilationUnit.DescendantNodes()
                .OfType<BaseTypeDeclarationSyntax>()
                .Select(MapFileType)
                .Concat(compilationUnit.DescendantNodes().OfType<DelegateDeclarationSyntax>().Select(MapFileType))
                .OrderBy(static type => type.Start.Line)
                .ThenBy(static type => type.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new GetFileOverviewResult(document.FilePath, namespaces, types, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to retrieve file overview for {FilePath}", request.FilePath);
            return new GetFileOverviewResult(null, Array.Empty<string>(), Array.Empty<FileTypeDescriptor>(),
                "The server could not retrieve the file overview.", "Retry the request and ensure the solution is loaded.");
        }
    }

    private static IEnumerable<string> GetNamespaces(MemberDeclarationSyntax member) =>
        member switch
        {
            NamespaceDeclarationSyntax namespaceDeclaration => [namespaceDeclaration.Name.ToString()],
            FileScopedNamespaceDeclarationSyntax fileScopedNamespace => [fileScopedNamespace.Name.ToString()],
            _ => []
        };

    private static FileTypeDescriptor MapFileType(BaseTypeDeclarationSyntax typeDeclaration)
    {
        var lineSpan = typeDeclaration.GetLocation().GetLineSpan();
        var memberCount = typeDeclaration is TypeDeclarationSyntax typeSyntax
            ? typeSyntax.Members.Count
            : typeDeclaration is EnumDeclarationSyntax enumSyntax
                ? enumSyntax.Members.Count
                : 0;

        return new FileTypeDescriptor(
            Name: typeDeclaration.Identifier.Text,
            Kind: GetBaseTypeKind(typeDeclaration),
            Start: new SymbolLocation(typeDeclaration.SyntaxTree.FilePath, lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1),
            MemberCount: memberCount);
    }

    private static FileTypeDescriptor MapFileType(DelegateDeclarationSyntax delegateDeclaration)
    {
        var lineSpan = delegateDeclaration.GetLocation().GetLineSpan();
        return new FileTypeDescriptor(
            Name: delegateDeclaration.Identifier.Text,
            Kind: "Delegate",
            Start: new SymbolLocation(delegateDeclaration.SyntaxTree.FilePath, lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1),
            MemberCount: 0);
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
