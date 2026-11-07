using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>
/// Shares symbol resolution and descriptor mapping across Roslyn navigation data providers.
/// </summary>
internal sealed class SymbolResolutionHelper(IWorkspaceSessionProvider workspaceSessionProvider)
{
    public async ValueTask<ResolvedSymbol> ResolveSymbolAsync(
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
                Document: null,
                Symbol: null,
                FailureReason: "The requested solution is not currently loaded.",
                Guidance: "Call load_solution first for the requested solutionPath.");
        }

        var document = FindDocument(session, filePath);
        if (document is null)
        {
            return new ResolvedSymbol(
                Session: session,
                Document: null,
                Symbol: null,
                FailureReason: "The requested file was not found in the loaded solution.",
                Guidance: "Call get_project_structure with includeDocuments=true to inspect valid file paths.");
        }

        var sourceText = await document.GetTextAsync(cancellationToken);
        if (!TryGetPosition(sourceText, line, column, out var position, out var error))
        {
            return new ResolvedSymbol(
                Session: session,
                Document: document,
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
                Document: document,
                Symbol: null,
                FailureReason: "The requested document does not have semantic information available.",
                Guidance: "Use a source document that belongs to a loaded C# project.");
        }

        var symbol = ResolveSymbol(root, semanticModel, position, cancellationToken);
        if (symbol is null)
        {
            return new ResolvedSymbol(
                Session: session,
                Document: document,
                Symbol: null,
                FailureReason: "No symbol was resolved at the requested source position.",
                Guidance: "Move the cursor onto a declaration or reference token and try again.");
        }

        return new ResolvedSymbol(session, document, symbol, null, null);
    }

    public static Document? FindDocument(RoslynWorkspaceSession session, string filePath)
    {
        var normalizedPath = NormalizePath(filePath);
        return session.Solution.Projects
            .SelectMany(static project => project.Documents)
            .FirstOrDefault(document =>
                document.FilePath is not null &&
                Path.GetFullPath(document.FilePath).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryGetPosition(
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

    public static ISymbol? ResolveSymbol(
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

    public static IEnumerable<SymbolLocation> GetSourceLocations(ISymbol symbol) =>
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

    public static SymbolDescriptor? MapSymbol(ISymbol? symbol)
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

    public static string NormalizePath(string path) => Path.GetFullPath(path);

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
}

internal sealed record ResolvedSymbol(
    RoslynWorkspaceSession? Session,
    Document? Document,
    ISymbol? Symbol,
    string? FailureReason,
    string? Guidance);
