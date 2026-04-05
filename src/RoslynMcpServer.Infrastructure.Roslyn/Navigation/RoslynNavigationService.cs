using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>
/// Implements read-only semantic navigation over loaded Roslyn solutions.
/// </summary>
internal sealed class RoslynNavigationService(
    IWorkspaceSessionProvider workspaceSessionProvider,
    ILogger<RoslynNavigationService> logger) : INavigationService
{
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

        var document = FindDocument(session, request.FilePath);
        if (document is null)
        {
            return new GetDocumentOutlineResult(
                FilePath: NormalizePath(request.FilePath),
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

    public async ValueTask<SearchSymbolsResult> SearchSymbolsAsync(
        SearchSymbolsRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new SearchSymbolsResult(
                Symbols: Array.Empty<SymbolDescriptor>(),
                FailureReason: "A non-empty query is required.",
                Guidance: "Pass part of a symbol name and optionally a kindFilter.");
        }

        var session = await workspaceSessionProvider.GetReadySessionAsync(request.SolutionPath, cancellationToken);
        if (session is null)
        {
            return new SearchSymbolsResult(
                Symbols: Array.Empty<SymbolDescriptor>(),
                FailureReason: "The requested solution is not currently loaded.",
                Guidance: "Call load_solution first for the requested solutionPath.");
        }

        var symbols = await SymbolFinder.FindSourceDeclarationsAsync(
            session.Solution,
            name => name.Contains(request.Query, StringComparison.OrdinalIgnoreCase),
            SymbolFilter.TypeAndMember,
            cancellationToken);

        var results = symbols
            .Select(MapSymbol)
            .Where(static descriptor => descriptor is not null)
            .Cast<SymbolDescriptor>()
            .Where(descriptor => MatchesKindFilter(descriptor, request.KindFilter))
            .OrderBy(static descriptor => descriptor.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static descriptor => descriptor.ContainerName, StringComparer.OrdinalIgnoreCase)
            .Take(NormalizeMaxResults(request.MaxResults))
            .ToArray();

        return new SearchSymbolsResult(results, null, null);
    }

    public async ValueTask<GetSymbolInfoResult> GetSymbolInfoAsync(
        GetSymbolInfoRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resolved = await ResolveSymbolAsync(
            request.SolutionPath,
            request.FilePath,
            request.Line,
            request.Column,
            cancellationToken);

        if (resolved.FailureReason is not null)
        {
            return new GetSymbolInfoResult(null, resolved.FailureReason, resolved.Guidance);
        }

        return new GetSymbolInfoResult(MapSymbol(resolved.Symbol), null, null);
    }

    public async ValueTask<GoToDefinitionResult> GoToDefinitionAsync(
        GoToDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resolved = await ResolveSymbolAsync(
            request.SolutionPath,
            request.FilePath,
            request.Line,
            request.Column,
            cancellationToken);

        if (resolved.FailureReason is not null)
        {
            return new GoToDefinitionResult(null, Array.Empty<SymbolLocation>(), resolved.FailureReason, resolved.Guidance);
        }

        if (resolved.Symbol is null)
        {
            return new GoToDefinitionResult(
                Symbol: null,
                Definitions: Array.Empty<SymbolLocation>(),
                FailureReason: "No symbol was resolved at the requested source position.",
                Guidance: "Move the cursor onto a declaration or reference token and try again.");
        }

        var definitions = GetSourceLocations(resolved.Symbol).ToArray();
        if (definitions.Length == 0)
        {
            return new GoToDefinitionResult(
                Symbol: MapSymbol(resolved.Symbol),
                Definitions: Array.Empty<SymbolLocation>(),
                FailureReason: "No source definition was found for the resolved symbol.",
                Guidance: "The symbol may come from metadata or generated code outside the loaded solution.");
        }

        return new GoToDefinitionResult(MapSymbol(resolved.Symbol), definitions, null, null);
    }

    public async ValueTask<FindReferencesResult> FindReferencesAsync(
        FindReferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resolved = await ResolveSymbolAsync(
            request.SolutionPath,
            request.FilePath,
            request.Line,
            request.Column,
            cancellationToken);

        if (resolved.FailureReason is not null)
        {
            return new FindReferencesResult(null, Array.Empty<SymbolReferenceDescriptor>(), resolved.FailureReason, resolved.Guidance);
        }

        try
        {
            if (resolved.Symbol is null || resolved.Session is null)
            {
                return new FindReferencesResult(
                    Symbol: null,
                    References: Array.Empty<SymbolReferenceDescriptor>(),
                    FailureReason: "No symbol was resolved at the requested source position.",
                    Guidance: "Move the cursor onto a declaration or reference token and try again.");
            }

            var referencedSymbols = await SymbolFinder.FindReferencesAsync(
                resolved.Symbol,
                resolved.Session.Solution,
                cancellationToken);

            var maxResults = NormalizeMaxResults(request.MaxResults);
            var references = new List<SymbolReferenceDescriptor>(Math.Min(maxResults, 64));

            foreach (var referencedSymbol in referencedSymbols)
            {
                foreach (var location in referencedSymbol.Locations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var descriptor = await MapReferenceAsync(location, cancellationToken);
                    if (descriptor is null)
                    {
                        continue;
                    }

                    references.Add(descriptor);
                    if (references.Count >= maxResults)
                    {
                        return new FindReferencesResult(MapSymbol(resolved.Symbol), references, null, null);
                    }
                }
            }

            return new FindReferencesResult(MapSymbol(resolved.Symbol), references, null, null);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to find references for {FilePath}:{Line}:{Column}", request.FilePath, request.Line, request.Column);

            return new FindReferencesResult(
                Symbol: MapSymbol(resolved.Symbol),
                References: Array.Empty<SymbolReferenceDescriptor>(),
                FailureReason: "The server could not compute references for the resolved symbol.",
                Guidance: "Retry the request and confirm the file position resolves to a source symbol.");
        }
    }

    private async ValueTask<ResolvedSymbol> ResolveSymbolAsync(
        string solutionPath,
        string filePath,
        int line,
        int column,
        CancellationToken cancellationToken)
    {
        var session = await workspaceSessionProvider.GetReadySessionAsync(solutionPath, cancellationToken);
        if (session is null)
        {
            return new ResolvedSymbol(
                Session: null,
                Symbol: null,
                FailureReason: "The requested solution is not currently loaded.",
                Guidance: "Call load_solution first for the requested solutionPath.");
        }

        var document = FindDocument(session, filePath);
        if (document is null)
        {
            return new ResolvedSymbol(
                Session: session,
                Symbol: null,
                FailureReason: "The requested file was not found in the loaded solution.",
                Guidance: "Call get_project_structure with includeDocuments=true to inspect valid file paths.");
        }

        var sourceText = await document.GetTextAsync(cancellationToken);
        if (!TryGetPosition(sourceText, line, column, out var position, out var error))
        {
            return new ResolvedSymbol(
                Session: session,
                Symbol: null,
                FailureReason: error,
                Guidance: "Use 1-based line and column values within the requested file.");
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (root is null || semanticModel is null)
        {
            return new ResolvedSymbol(
                Session: session,
                Symbol: null,
                FailureReason: "The requested document does not have semantic information available.",
                Guidance: "Use a source document that belongs to a loaded C# project.");
        }

        var symbol = ResolveSymbol(root, semanticModel, position, cancellationToken);
        if (symbol is null)
        {
            return new ResolvedSymbol(
                Session: session,
                Symbol: null,
                FailureReason: "No symbol was resolved at the requested source position.",
                Guidance: "Move the cursor onto a declaration or reference token and try again.");
        }

        return new ResolvedSymbol(session, symbol, null, null);
    }

    private static async ValueTask<SymbolReferenceDescriptor?> MapReferenceAsync(
        ReferenceLocation location,
        CancellationToken cancellationToken)
    {
        var lineSpan = location.Location.GetLineSpan();
        var sourceTree = location.Location.SourceTree;
        var sourceText = sourceTree is null
            ? null
            : await sourceTree.GetTextAsync(cancellationToken);

        string? lineText = null;
        if (sourceText is not null && lineSpan.StartLinePosition.Line < sourceText.Lines.Count)
        {
            lineText = sourceText.Lines[lineSpan.StartLinePosition.Line].ToString().Trim();
        }

        return new SymbolReferenceDescriptor(
            Location: new SymbolLocation(
                location.Document.FilePath,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1),
            ContainingSymbol: null,
            LineText: lineText);
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

    private static bool MatchesKindFilter(SymbolDescriptor descriptor, string? kindFilter) =>
        string.IsNullOrWhiteSpace(kindFilter) ||
        descriptor.Kind.Equals(kindFilter, StringComparison.OrdinalIgnoreCase);

    private static int NormalizeMaxResults(int maxResults) =>
        maxResults <= 0
            ? 50
            : Math.Min(maxResults, 200);

    private static Document? FindDocument(RoslynWorkspaceSession session, string filePath)
    {
        var normalizedPath = NormalizePath(filePath);
        return session.Solution.Projects
            .SelectMany(static project => project.Documents)
            .FirstOrDefault(document =>
                document.FilePath is not null &&
                Path.GetFullPath(document.FilePath).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetPosition(
        SourceText sourceText,
        int line,
        int column,
        out int position,
        out string? error)
    {
        position = 0;
        error = null;

        if (line <= 0 || column <= 0)
        {
            error = "Line and column must be positive 1-based values.";
            return false;
        }

        var lineIndex = line - 1;
        if (lineIndex >= sourceText.Lines.Count)
        {
            error = "The requested line is outside the document.";
            return false;
        }

        var textLine = sourceText.Lines[lineIndex];
        var maxColumn = textLine.End - textLine.Start + 1;
        if (column > maxColumn)
        {
            error = "The requested column is outside the target line.";
            return false;
        }

        position = textLine.Start + column - 1;
        return true;
    }

    private static ISymbol? ResolveSymbol(
        SyntaxNode root,
        SemanticModel semanticModel,
        int position,
        CancellationToken cancellationToken)
    {
        if (root.FullSpan.Length == 0)
        {
            return null;
        }

        var safePosition = Math.Min(position, root.FullSpan.End - 1);
        var token = root.FindToken(safePosition);

        for (var current = token.Parent; current is not null; current = current.Parent)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var info = semanticModel.GetSymbolInfo(current, cancellationToken);
            var symbol = semanticModel.GetDeclaredSymbol(current, cancellationToken)
                ?? info.Symbol
                ?? info.CandidateSymbols.FirstOrDefault();

            if (symbol is not null)
            {
                return symbol;
            }
        }

        return semanticModel.GetEnclosingSymbol(position, cancellationToken);
    }

    private static IEnumerable<SymbolLocation> GetSourceLocations(ISymbol symbol) =>
        symbol.Locations
            .Where(static location => location.IsInSource)
            .Select(static location =>
            {
                var lineSpan = location.GetLineSpan();
                return new SymbolLocation(
                    lineSpan.Path,
                    lineSpan.StartLinePosition.Line + 1,
                    lineSpan.StartLinePosition.Character + 1);
            });

    private static SymbolDescriptor? MapSymbol(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }

        var kind = GetSymbolKindName(symbol);
        if (kind is null)
        {
            return null;
        }

        return new SymbolDescriptor(
            Name: symbol.Name,
            Kind: kind,
            DisplayName: symbol.ToDisplayString(),
            ContainerName: GetContainerName(symbol),
            Definition: GetSourceLocations(symbol).FirstOrDefault());
    }

    private static string? GetContainerName(ISymbol symbol)
    {
        if (symbol.ContainingType is not null)
        {
            return symbol.ContainingType.ToDisplayString();
        }

        var containingNamespace = symbol.ContainingNamespace?.ToDisplayString();
        return string.IsNullOrWhiteSpace(containingNamespace)
            ? null
            : containingNamespace;
    }

    private static string? GetSymbolKindName(ISymbol symbol) =>
        symbol switch
        {
            INamedTypeSymbol namedType when namedType.IsRecord => "Record",
            INamedTypeSymbol { TypeKind: TypeKind.Class } => "Class",
            INamedTypeSymbol { TypeKind: TypeKind.Struct } => "Struct",
            INamedTypeSymbol { TypeKind: TypeKind.Interface } => "Interface",
            INamedTypeSymbol { TypeKind: TypeKind.Enum } => "Enum",
            INamedTypeSymbol { TypeKind: TypeKind.Delegate } => "Delegate",
            IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } => "Constructor",
            IMethodSymbol { MethodKind: MethodKind.Destructor } => "Destructor",
            IMethodSymbol => "Method",
            IPropertySymbol => "Property",
            IFieldSymbol { IsConst: true } => "Constant",
            IFieldSymbol => "Field",
            IEventSymbol => "Event",
            _ => null
        };

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    private sealed record ResolvedSymbol(
        RoslynWorkspaceSession? Session,
        ISymbol? Symbol,
        string? FailureReason,
        string? Guidance);
}
