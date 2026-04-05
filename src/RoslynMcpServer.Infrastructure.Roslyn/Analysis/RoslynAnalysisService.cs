using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>
/// Implements read-only semantic analysis over loaded Roslyn solutions.
/// </summary>
internal sealed class RoslynAnalysisService(
    IWorkspaceSessionProvider workspaceSessionProvider,
    ILogger<RoslynAnalysisService> logger) : IAnalysisService
{
    public async ValueTask<GetDiagnosticsResult> GetDiagnosticsAsync(
        GetDiagnosticsRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var session = await workspaceSessionProvider.GetReadySessionAsync(request.SolutionPath, cancellationToken);
        if (session is null)
        {
            return new GetDiagnosticsResult(
                Diagnostics: Array.Empty<AnalysisDiagnosticDescriptor>(),
                FailureReason: "The requested solution is not currently loaded.",
                Guidance: "Call load_solution first for the requested solutionPath.");
        }

        var normalizedFilePath = string.IsNullOrWhiteSpace(request.FilePath)
            ? null
            : NormalizePath(request.FilePath);

        if (normalizedFilePath is not null && FindDocument(session, normalizedFilePath) is null)
        {
            return new GetDiagnosticsResult(
                Diagnostics: Array.Empty<AnalysisDiagnosticDescriptor>(),
                FailureReason: "The requested file was not found in the loaded solution.",
                Guidance: "Call get_project_structure with includeDocuments=true to inspect valid file paths.");
        }

        var diagnostics = new List<AnalysisDiagnosticDescriptor>();

        foreach (var project in session.Solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
            {
                continue;
            }

            foreach (var diagnostic in compilation.GetDiagnostics(cancellationToken))
            {
                if (!MatchesSeverityFilter(diagnostic, request.SeverityFilter))
                {
                    continue;
                }

                var location = MapLocation(diagnostic.Location);
                if (normalizedFilePath is not null &&
                    !string.Equals(location?.FilePath, normalizedFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                diagnostics.Add(new AnalysisDiagnosticDescriptor(
                    diagnostic.Id,
                    diagnostic.Severity.ToString(),
                    diagnostic.GetMessage(),
                    location));
            }
        }

        return new GetDiagnosticsResult(
            Diagnostics: diagnostics
                .OrderBy(static diagnostic => diagnostic.Location?.FilePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static diagnostic => diagnostic.Location?.Line ?? int.MaxValue)
                .ThenBy(static diagnostic => diagnostic.Location?.Column ?? int.MaxValue)
                .ToArray(),
            FailureReason: null,
            Guidance: null);
    }

    public async ValueTask<FindImplementationsResult> FindImplementationsAsync(
        FindImplementationsRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resolved = await ResolveSymbolAsync(request.SolutionPath, request.FilePath, request.Line, request.Column, cancellationToken);
        if (resolved.FailureReason is not null)
        {
            return new FindImplementationsResult(null, Array.Empty<SymbolDescriptor>(), resolved.FailureReason, resolved.Guidance);
        }

        if (resolved.Symbol is null || resolved.Session is null)
        {
            return new FindImplementationsResult(
                Symbol: null,
                Implementations: Array.Empty<SymbolDescriptor>(),
                FailureReason: "No symbol was resolved at the requested source position.",
                Guidance: "Move the cursor onto an interface, abstract member, or type declaration and try again.");
        }

        try
        {
            var implementations = await SymbolFinder.FindImplementationsAsync(
                resolved.Symbol,
                resolved.Session.Solution,
                cancellationToken: cancellationToken);

            var results = implementations
                .Select(MapSymbol)
                .Where(static descriptor => descriptor is not null)
                .Cast<SymbolDescriptor>()
                .DistinctBy(static descriptor => $"{descriptor.Definition?.FilePath}:{descriptor.Definition?.Line}:{descriptor.Definition?.Column}:{descriptor.DisplayName}")
                .Take(NormalizeMaxResults(request.MaxResults))
                .ToArray();

            return new FindImplementationsResult(MapSymbol(resolved.Symbol), results, null, null);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to find implementations for {FilePath}:{Line}:{Column}", request.FilePath, request.Line, request.Column);
            return new FindImplementationsResult(
                Symbol: MapSymbol(resolved.Symbol),
                Implementations: Array.Empty<SymbolDescriptor>(),
                FailureReason: "The server could not compute implementations for the resolved symbol.",
                Guidance: "Retry the request on an interface, abstract member, or base type.");
        }
    }

    public async ValueTask<GetTypeHierarchyResult> GetTypeHierarchyAsync(
        GetTypeHierarchyRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resolved = await ResolveSymbolAsync(request.SolutionPath, request.FilePath, request.Line, request.Column, cancellationToken);
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
                Symbol: MapSymbol(resolved.Symbol),
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
            ? EnumerateBaseTypes(typeSymbol).Select(MapSymbol).Where(static descriptor => descriptor is not null).Cast<SymbolDescriptor>().ToArray()
            : Array.Empty<SymbolDescriptor>();

        var derivedTypes = includeDescendants
            ? await FindDerivedTypesAsync(typeSymbol, resolved.Session?.Solution, cancellationToken)
            : Array.Empty<SymbolDescriptor>();

        var interfaces = includeAncestors
            ? typeSymbol.AllInterfaces.Select(MapSymbol).Where(static descriptor => descriptor is not null).Cast<SymbolDescriptor>().ToArray()
            : Array.Empty<SymbolDescriptor>();

        return new GetTypeHierarchyResult(MapSymbol(typeSymbol), baseTypes, derivedTypes, interfaces, null, null);
    }

    public async ValueTask<FindCallersResult> FindCallersAsync(
        FindCallersRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resolved = await ResolveSymbolAsync(request.SolutionPath, request.FilePath, request.Line, request.Column, cancellationToken);
        if (resolved.FailureReason is not null)
        {
            return new FindCallersResult(null, Array.Empty<CallerDescriptor>(), resolved.FailureReason, resolved.Guidance);
        }

        if (resolved.Symbol is null || resolved.Session is null)
        {
            return new FindCallersResult(
                Symbol: null,
                Callers: Array.Empty<CallerDescriptor>(),
                FailureReason: "No symbol was resolved at the requested source position.",
                Guidance: "Move the cursor onto a method, property, or other callable member and try again.");
        }

        try
        {
            var callers = await SymbolFinder.FindCallersAsync(resolved.Symbol, resolved.Session.Solution, cancellationToken);
            var results = new List<CallerDescriptor>();
            var maxResults = NormalizeMaxResults(request.MaxResults);

            foreach (var caller in callers)
            {
                foreach (var location in caller.Locations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var descriptor = await MapCallerAsync(caller.CallingSymbol?.ToDisplayString(), location, cancellationToken);
                    if (descriptor is null)
                    {
                        continue;
                    }

                    results.Add(descriptor);
                    if (results.Count >= maxResults)
                    {
                        return new FindCallersResult(MapSymbol(resolved.Symbol), results, null, null);
                    }
                }
            }

            return new FindCallersResult(MapSymbol(resolved.Symbol), results, null, null);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to find callers for {FilePath}:{Line}:{Column}", request.FilePath, request.Line, request.Column);
            return new FindCallersResult(
                Symbol: MapSymbol(resolved.Symbol),
                Callers: Array.Empty<CallerDescriptor>(),
                FailureReason: "The server could not compute callers for the resolved symbol.",
                Guidance: "Retry the request on a callable symbol inside the loaded solution.");
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
            return new ResolvedSymbol(null, null, null, "The requested solution is not currently loaded.", "Call load_solution first for the requested solutionPath.");
        }

        var document = FindDocument(session, filePath);
        if (document is null)
        {
            return new ResolvedSymbol(session, null, null, "The requested file was not found in the loaded solution.", "Call get_project_structure with includeDocuments=true to inspect valid file paths.");
        }

        var sourceText = await document.GetTextAsync(cancellationToken);
        if (!TryGetPosition(sourceText, line, column, out var position, out var error))
        {
            return new ResolvedSymbol(session, document, null, error, "Use 1-based line and column values within the requested file.");
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (root is null || semanticModel is null)
        {
            return new ResolvedSymbol(session, document, null, "The requested document does not have semantic information available.", "Use a source document that belongs to a loaded C# project.");
        }

        var symbol = ResolveSymbol(root, semanticModel, position, cancellationToken);
        if (symbol is null)
        {
            return new ResolvedSymbol(session, document, null, "No symbol was resolved at the requested source position.", "Move the cursor onto a declaration or reference token and try again.");
        }

        return new ResolvedSymbol(session, document, symbol, null, null);
    }

    private static async Task<CallerDescriptor?> MapCallerAsync(
        string? callingSymbol,
        Location location,
        CancellationToken cancellationToken)
    {
        if (!location.IsInSource)
        {
            return null;
        }

        var lineSpan = location.GetLineSpan();
        var sourceTree = location.SourceTree;
        var sourceText = sourceTree is null ? null : await sourceTree.GetTextAsync(cancellationToken);

        string? lineText = null;
        if (sourceText is not null && lineSpan.StartLinePosition.Line < sourceText.Lines.Count)
        {
            lineText = sourceText.Lines[lineSpan.StartLinePosition.Line].ToString().Trim();
        }

        return new CallerDescriptor(
            callingSymbol,
            new SymbolLocation(lineSpan.Path, lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1),
            lineText);
    }

    private static bool MatchesSeverityFilter(Diagnostic diagnostic, string? severityFilter)
    {
        if (string.IsNullOrWhiteSpace(severityFilter) || severityFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return diagnostic.Severity switch
        {
            DiagnosticSeverity.Error => severityFilter.Equals("Error", StringComparison.OrdinalIgnoreCase),
            DiagnosticSeverity.Warning => severityFilter.Equals("Warning", StringComparison.OrdinalIgnoreCase),
            DiagnosticSeverity.Info => severityFilter.Equals("Info", StringComparison.OrdinalIgnoreCase),
            DiagnosticSeverity.Hidden => severityFilter.Equals("Hidden", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static SymbolLocation? MapLocation(Location location)
    {
        if (!location.IsInSource)
        {
            return null;
        }

        var lineSpan = location.GetLineSpan();
        return new SymbolLocation(
            lineSpan.Path,
            lineSpan.StartLinePosition.Line + 1,
            lineSpan.StartLinePosition.Character + 1);
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
            .Select(MapSymbol)
            .Where(static descriptor => descriptor is not null)
            .Cast<SymbolDescriptor>()
            .DistinctBy(static descriptor => $"{descriptor.Definition?.FilePath}:{descriptor.Definition?.Line}:{descriptor.Definition?.Column}:{descriptor.DisplayName}")
            .ToArray();
    }

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

    private static SymbolDescriptor? MapSymbol(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }

        var kind = symbol switch
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

        if (kind is null)
        {
            return null;
        }

        var location = symbol.Locations
            .Where(static entry => entry.IsInSource)
            .Select(MapLocation)
            .FirstOrDefault();

        return new SymbolDescriptor(
            symbol.Name,
            kind,
            symbol.ToDisplayString(),
            symbol.ContainingType?.ToDisplayString() ?? NullIfWhiteSpace(symbol.ContainingNamespace?.ToDisplayString()),
            location);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static int NormalizeMaxResults(int maxResults) =>
        maxResults <= 0
            ? 50
            : Math.Min(maxResults, 200);

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    private sealed record ResolvedSymbol(
        RoslynWorkspaceSession? Session,
        Document? Document,
        ISymbol? Symbol,
        string? FailureReason,
        string? Guidance);
}
