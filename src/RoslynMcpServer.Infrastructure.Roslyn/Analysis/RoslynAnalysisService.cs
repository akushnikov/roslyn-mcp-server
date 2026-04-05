using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

    public async ValueTask<GetOutgoingCallsResult> GetOutgoingCallsAsync(
        GetOutgoingCallsRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resolved = await ResolveSymbolAsync(request.SolutionPath, request.FilePath, request.Line, request.Column, cancellationToken);
        if (resolved.FailureReason is not null)
        {
            return new GetOutgoingCallsResult(null, Array.Empty<CallEdgeDescriptor>(), resolved.FailureReason, resolved.Guidance);
        }

        var callableSymbol = resolved.Symbol switch
        {
            IMethodSymbol method => method,
            IPropertySymbol property => property.GetMethod ?? property.SetMethod,
            IEventSymbol eventSymbol => eventSymbol.AddMethod ?? eventSymbol.RemoveMethod,
            _ => null
        };

        if (callableSymbol is null || resolved.Document is null)
        {
            return new GetOutgoingCallsResult(
                Symbol: MapSymbol(resolved.Symbol),
                Calls: Array.Empty<CallEdgeDescriptor>(),
                FailureReason: "The resolved symbol does not have an analyzable callable body.",
                Guidance: "Use a source method, constructor, local function, property accessor, or event accessor.");
        }

        var declarationNode = await GetCallableDeclarationNodeAsync(callableSymbol, cancellationToken);
        if (declarationNode is null)
        {
            return new GetOutgoingCallsResult(
                Symbol: MapSymbol(callableSymbol),
                Calls: Array.Empty<CallEdgeDescriptor>(),
                FailureReason: "The resolved callable symbol does not have source syntax available.",
                Guidance: "Use a callable member declared in a source document of the loaded solution.");
        }

        var semanticModel = await resolved.Document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel is null)
        {
            return new GetOutgoingCallsResult(
                Symbol: MapSymbol(callableSymbol),
                Calls: Array.Empty<CallEdgeDescriptor>(),
                FailureReason: "The requested document does not have semantic information available.",
                Guidance: "Use a source document that belongs to a loaded C# project.");
        }

        var calls = new List<CallEdgeDescriptor>();
        foreach (var invocation in declarationNode.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
            var location = MapSyntaxLocation(invocation);
            if (symbol is null || location is null)
            {
                continue;
            }

            calls.Add(new CallEdgeDescriptor(MapSymbol(symbol), location, invocation.ToString()));
        }

        foreach (var creation in declarationNode.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = semanticModel.GetSymbolInfo(creation, cancellationToken).Symbol as IMethodSymbol;
            var location = MapSyntaxLocation(creation);
            if (symbol is null || location is null)
            {
                continue;
            }

            calls.Add(new CallEdgeDescriptor(MapSymbol(symbol), location, creation.ToString()));
        }

        var results = calls
            .DistinctBy(static call => $"{call.Location.FilePath}:{call.Location.Line}:{call.Location.Column}:{call.Target?.DisplayName}")
            .Take(NormalizeMaxResults(request.MaxResults))
            .ToArray();

        return new GetOutgoingCallsResult(MapSymbol(callableSymbol), results, null, null);
    }

    public async ValueTask<ValidateCodeResult> ValidateCodeAsync(
        ValidateCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = await GetDiagnosticsAsync(
            new GetDiagnosticsRequest(request.SolutionPath, request.FilePath, "All"),
            cancellationToken);

        if (diagnostics.FailureReason is not null)
        {
            return new ValidateCodeResult(false, 0, 0, Array.Empty<AnalysisDiagnosticDescriptor>(), diagnostics.FailureReason, diagnostics.Guidance);
        }

        var errorCount = diagnostics.Diagnostics.Count(static diagnostic => diagnostic.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase));
        var warningCount = diagnostics.Diagnostics.Count(static diagnostic => diagnostic.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase));

        return new ValidateCodeResult(
            IsValid: errorCount == 0,
            ErrorCount: errorCount,
            WarningCount: warningCount,
            Diagnostics: diagnostics.Diagnostics,
            FailureReason: null,
            Guidance: errorCount == 0 ? null : "Resolve compiler errors before applying additional semantic edits.");
    }

    public async ValueTask<AnalyzeMethodResult> AnalyzeMethodAsync(
        AnalyzeMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resolved = await ResolveSymbolAsync(request.SolutionPath, request.FilePath, request.Line, request.Column, cancellationToken);
        if (resolved.FailureReason is not null)
        {
            return new AnalyzeMethodResult(null, null, null, Array.Empty<CallEdgeDescriptor>(), resolved.FailureReason, resolved.Guidance);
        }

        var methodSymbol = resolved.Symbol switch
        {
            IMethodSymbol method => method,
            IPropertySymbol property => property.GetMethod ?? property.SetMethod,
            IEventSymbol eventSymbol => eventSymbol.AddMethod ?? eventSymbol.RemoveMethod,
            _ => null
        };

        if (methodSymbol is null || resolved.Document is null)
        {
            return new AnalyzeMethodResult(
                Signature: null,
                DataFlow: null,
                ControlFlow: null,
                OutgoingCalls: Array.Empty<CallEdgeDescriptor>(),
                FailureReason: "The resolved symbol does not have an analyzable callable body.",
                Guidance: "Use a source method, constructor, local function, property accessor, or event accessor.");
        }

        var region = await ResolveMethodAnalysisRegionAsync(resolved.Document, methodSymbol, cancellationToken);
        if (region.FailureReason is not null)
        {
            return new AnalyzeMethodResult(
                Signature: MapMethodSignature(methodSymbol),
                DataFlow: null,
                ControlFlow: null,
                OutgoingCalls: Array.Empty<CallEdgeDescriptor>(),
                FailureReason: region.FailureReason,
                Guidance: region.Guidance);
        }

        var semanticModel = region.SemanticModel!;
        var firstStatement = region.FirstStatement!;
        var lastStatement = region.LastStatement!;

        var dataFlow = BuildDataFlowDescriptor(semanticModel.AnalyzeDataFlow(firstStatement, lastStatement));
        var controlFlow = BuildControlFlowDescriptor(semanticModel.AnalyzeControlFlow(firstStatement, lastStatement));
        var outgoingCalls = await BuildOutgoingCallsAsync(methodSymbol, resolved.Document, request.MaxOutgoingCalls, cancellationToken);

        return new AnalyzeMethodResult(
            Signature: MapMethodSignature(methodSymbol),
            DataFlow: dataFlow,
            ControlFlow: controlFlow,
            OutgoingCalls: outgoingCalls,
            FailureReason: null,
            Guidance: null);
    }

    public async ValueTask<AnalyzeChangeImpactResult> AnalyzeChangeImpactAsync(
        AnalyzeChangeImpactRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resolved = await ResolveSymbolAsync(request.SolutionPath, request.FilePath, request.Line, request.Column, cancellationToken);
        if (resolved.FailureReason is not null)
        {
            return new AnalyzeChangeImpactResult(null, null, resolved.FailureReason, resolved.Guidance);
        }

        if (resolved.Symbol is null || resolved.Session is null)
        {
            return new AnalyzeChangeImpactResult(null, null, "No symbol was resolved at the requested source position.", "Move the cursor onto a declaration or reference token and try again.");
        }

        try
        {
            var maxResults = NormalizeMaxResults(request.MaxResults);

            var references = new List<SymbolReferenceDescriptor>();
            var referencedSymbols = await SymbolFinder.FindReferencesAsync(resolved.Symbol, resolved.Session.Solution, cancellationToken);
            foreach (var referencedSymbol in referencedSymbols)
            {
                foreach (var location in referencedSymbol.Locations)
                {
                    var descriptor = await MapReferenceAsync(location, cancellationToken);
                    if (descriptor is null)
                    {
                        continue;
                    }

                    references.Add(descriptor);
                    if (references.Count >= maxResults)
                    {
                        break;
                    }
                }

                if (references.Count >= maxResults)
                {
                    break;
                }
            }

            var implementations = await SymbolFinder.FindImplementationsAsync(
                resolved.Symbol,
                resolved.Session.Solution,
                cancellationToken: cancellationToken);

            var implementationDescriptors = implementations
                .Select(MapSymbol)
                .Where(static symbol => symbol is not null)
                .Cast<SymbolDescriptor>()
                .DistinctBy(static symbol => $"{symbol.Definition?.FilePath}:{symbol.Definition?.Line}:{symbol.Definition?.Column}:{symbol.DisplayName}")
                .Take(maxResults)
                .ToArray();

            var callers = new List<CallerDescriptor>();
            if (resolved.Symbol is IMethodSymbol or IPropertySymbol or IEventSymbol)
            {
                var callerInfos = await SymbolFinder.FindCallersAsync(resolved.Symbol, resolved.Session.Solution, cancellationToken);
                foreach (var caller in callerInfos)
                {
                    foreach (var location in caller.Locations)
                    {
                        var descriptor = await MapCallerAsync(caller.CallingSymbol?.ToDisplayString(), location, cancellationToken);
                        if (descriptor is null)
                        {
                            continue;
                        }

                        callers.Add(descriptor);
                        if (callers.Count >= maxResults)
                        {
                            break;
                        }
                    }

                    if (callers.Count >= maxResults)
                    {
                        break;
                    }
                }
            }

            var impactedFiles = references.Select(static entry => entry.Location.FilePath)
                .Concat(callers.Select(static entry => entry.Location.FilePath))
                .Concat(implementationDescriptors.Select(static entry => entry.Definition?.FilePath).Where(static path => !string.IsNullOrWhiteSpace(path))!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToArray();

            return new AnalyzeChangeImpactResult(
                Symbol: MapSymbol(resolved.Symbol),
                Impact: new ChangeImpactDescriptor(
                    ReferenceCount: references.Count,
                    CallerCount: callers.Count,
                    ImplementationCount: implementationDescriptors.Length,
                    ImpactedFiles: impactedFiles,
                    References: references,
                    Callers: callers,
                    Implementations: implementationDescriptors),
                FailureReason: null,
                Guidance: null);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to analyze change impact for {FilePath}:{Line}:{Column}", request.FilePath, request.Line, request.Column);
            return new AnalyzeChangeImpactResult(
                Symbol: MapSymbol(resolved.Symbol),
                Impact: null,
                FailureReason: "The server could not compute a semantic impact summary for the resolved symbol.",
                Guidance: "Retry the request on a source symbol inside the loaded solution.");
        }
    }

    public async ValueTask<CheckTypeCompatibilityResult> CheckTypeCompatibilityAsync(
        CheckTypeCompatibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var source = await ResolveSymbolAsync(request.SolutionPath, request.SourceFilePath, request.SourceLine, request.SourceColumn, cancellationToken);
        if (source.FailureReason is not null)
        {
            return new CheckTypeCompatibilityResult(null, null, null, source.FailureReason, source.Guidance);
        }

        var target = await ResolveSymbolAsync(request.SolutionPath, request.TargetFilePath, request.TargetLine, request.TargetColumn, cancellationToken);
        if (target.FailureReason is not null)
        {
            return new CheckTypeCompatibilityResult(MapSymbol(source.Symbol), null, null, target.FailureReason, target.Guidance);
        }

        var sourceType = GetTypeSymbol(source.Symbol);
        var targetType = GetTypeSymbol(target.Symbol);
        if (sourceType is null || targetType is null || source.Document is null)
        {
            return new CheckTypeCompatibilityResult(
                Source: MapSymbol(source.Symbol),
                Target: MapSymbol(target.Symbol),
                Compatibility: null,
                FailureReason: "The requested symbols do not both resolve to analyzable types.",
                Guidance: "Use type declarations, typed members, parameters, locals, or fields.");
        }

        var compatibility = ClassifyTypeCompatibility(sourceType, targetType);
        return new CheckTypeCompatibilityResult(
            Source: MapSymbol(source.Symbol),
            Target: MapSymbol(target.Symbol),
            Compatibility: compatibility,
            FailureReason: null,
            Guidance: null);
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
            new SymbolLocation(
                location.Document.FilePath,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1),
            null,
            lineText);
    }

    private async ValueTask<AnalysisRegion> ResolveAnalysisRegionAsync(
        string solutionPath,
        string filePath,
        int startLine,
        int endLine,
        CancellationToken cancellationToken)
    {
        var session = await workspaceSessionProvider.GetReadySessionAsync(solutionPath, cancellationToken);
        if (session is null)
        {
            return new AnalysisRegion(null, null, null, null, "The requested solution is not currently loaded.", "Call load_solution first for the requested solutionPath.");
        }

        var document = FindDocument(session, filePath);
        if (document is null)
        {
            return new AnalysisRegion(null, null, null, null, "The requested file was not found in the loaded solution.", "Call get_project_structure with includeDocuments=true to inspect valid file paths.");
        }

        if (startLine <= 0 || endLine <= 0 || endLine < startLine)
        {
            return new AnalysisRegion(null, null, null, null, "The requested line range is invalid.", "Use positive 1-based line values and keep endLine greater than or equal to startLine.");
        }

        var sourceText = await document.GetTextAsync(cancellationToken);
        if (endLine > sourceText.Lines.Count)
        {
            return new AnalysisRegion(null, null, null, null, "The requested line range is outside the document.", "Use a line range that stays within the target file.");
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (root is null || semanticModel is null)
        {
            return new AnalysisRegion(null, null, null, null, "The requested document does not have semantic information available.", "Use a source document that belongs to a loaded C# project.");
        }

        var startPosition = sourceText.Lines[startLine - 1].Start;
        var endPosition = sourceText.Lines[endLine - 1].End;

        var statements = root.DescendantNodes()
            .OfType<StatementSyntax>()
            .Where(statement => statement.SpanStart >= startPosition && statement.Span.End <= endPosition)
            .OrderBy(static statement => statement.SpanStart)
            .ToArray();

        if (statements.Length == 0)
        {
            return new AnalysisRegion(null, null, null, null, "The requested line range does not contain analyzable statements.", "Select a contiguous region containing one or more complete C# statements.");
        }

        return new AnalysisRegion(document, semanticModel, statements[0], statements[^1], null, null);
    }

    private async ValueTask<AnalysisRegion> ResolveMethodAnalysisRegionAsync(
        Document document,
        IMethodSymbol methodSymbol,
        CancellationToken cancellationToken)
    {
        var declarationNode = await GetCallableDeclarationNodeAsync(methodSymbol, cancellationToken);
        if (declarationNode is null)
        {
            return new AnalysisRegion(null, null, null, null, "The resolved callable symbol does not have source syntax available.", "Use a callable member declared in a source document of the loaded solution.");
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel is null)
        {
            return new AnalysisRegion(null, null, null, null, "The requested document does not have semantic information available.", "Use a source document that belongs to a loaded C# project.");
        }

        var statements = GetCallableBodyStatements(declarationNode);
        if (statements is null)
        {
            return new AnalysisRegion(
                null,
                null,
                null,
                null,
                "The resolved callable symbol does not contain a block body that supports flow analysis.",
                "Use a callable member with a block body, or move the cursor onto a method or accessor implementation instead of an expression-bodied member.");
        }

        if (statements.Value.Count == 0)
        {
            return new AnalysisRegion(null, null, null, null, "The resolved callable symbol does not contain analyzable statements.", "Use a callable member with a block body that contains one or more statements.");
        }

        return new AnalysisRegion(document, semanticModel, statements.Value[0], statements.Value[^1], null, null);
    }

    private async ValueTask<IReadOnlyList<CallEdgeDescriptor>> BuildOutgoingCallsAsync(
        IMethodSymbol callableSymbol,
        Document document,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var declarationNode = await GetCallableDeclarationNodeAsync(callableSymbol, cancellationToken);
        if (declarationNode is null)
        {
            return Array.Empty<CallEdgeDescriptor>();
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel is null)
        {
            return Array.Empty<CallEdgeDescriptor>();
        }

        var calls = new List<CallEdgeDescriptor>();
        foreach (var invocation in declarationNode.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
            var location = MapSyntaxLocation(invocation);
            if (symbol is null || location is null)
            {
                continue;
            }

            calls.Add(new CallEdgeDescriptor(MapSymbol(symbol), location, invocation.ToString()));
        }

        foreach (var creation in declarationNode.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = semanticModel.GetSymbolInfo(creation, cancellationToken).Symbol as IMethodSymbol;
            var location = MapSyntaxLocation(creation);
            if (symbol is null || location is null)
            {
                continue;
            }

            calls.Add(new CallEdgeDescriptor(MapSymbol(symbol), location, creation.ToString()));
        }

        return calls
            .DistinctBy(static call => $"{call.Location.FilePath}:{call.Location.Line}:{call.Location.Column}:{call.Target?.DisplayName}")
            .Take(NormalizeMaxResults(maxResults))
            .ToArray();
    }

    private static DataFlowDescriptor BuildDataFlowDescriptor(DataFlowAnalysis analysis) =>
        new(
            VariablesDeclared: analysis.VariablesDeclared.Select(static symbol => symbol.Name).Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
            DataFlowsIn: analysis.DataFlowsIn.Select(static symbol => symbol.Name).Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
            DataFlowsOut: analysis.DataFlowsOut.Select(static symbol => symbol.Name).Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
            ReadInside: analysis.ReadInside.Select(static symbol => symbol.Name).Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
            WrittenInside: analysis.WrittenInside.Select(static symbol => symbol.Name).Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
            ReadOutside: analysis.ReadOutside.Select(static symbol => symbol.Name).Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
            WrittenOutside: analysis.WrittenOutside.Select(static symbol => symbol.Name).Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
            Captured: analysis.Captured.Select(static symbol => symbol.Name).Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
            AlwaysAssigned: analysis.AlwaysAssigned.Select(static symbol => symbol.Name).Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
            Succeeded: analysis.Succeeded);

    private static ControlFlowDescriptor BuildControlFlowDescriptor(ControlFlowAnalysis analysis) =>
        new(
            StartPointIsReachable: analysis.StartPointIsReachable,
            EndPointIsReachable: analysis.EndPointIsReachable,
            EntryPoints: analysis.EntryPoints.Select(MapSyntaxLocation).Where(static location => location is not null).Cast<SymbolLocation>().ToArray(),
            ExitPoints: analysis.ExitPoints.Select(MapSyntaxLocation).Where(static location => location is not null).Cast<SymbolLocation>().ToArray(),
            ReturnStatements: analysis.ReturnStatements.Select(MapSyntaxLocation).Where(static location => location is not null).Cast<SymbolLocation>().ToArray(),
            Succeeded: analysis.Succeeded);

    private static MethodSignatureDescriptor MapMethodSignature(IMethodSymbol methodSymbol)
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
            Definition: methodSymbol.Locations.Where(static location => location.IsInSource).Select(MapLocation).FirstOrDefault());
    }

    private static MethodParameterDescriptor MapParameter(IParameterSymbol parameter) =>
        new(
            Name: parameter.Name,
            Type: parameter.Type.ToDisplayString(),
            RefKind: parameter.RefKind.ToString(),
            IsOptional: parameter.IsOptional,
            DefaultValue: parameter.IsOptional && parameter.HasExplicitDefaultValue
                ? parameter.ExplicitDefaultValue?.ToString() ?? "null"
                : null);

    private static SymbolLocation? MapSyntaxLocation(SyntaxNode node)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        return string.IsNullOrWhiteSpace(lineSpan.Path)
            ? null
            : new SymbolLocation(
                lineSpan.Path,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1);
    }

    private static async ValueTask<SyntaxNode?> GetCallableDeclarationNodeAsync(
        IMethodSymbol methodSymbol,
        CancellationToken cancellationToken)
    {
        foreach (var syntaxReference in methodSymbol.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await syntaxReference.GetSyntaxAsync(cancellationToken) is SyntaxNode syntaxNode)
            {
                return syntaxNode;
            }
        }

        return null;
    }

    private static SyntaxList<StatementSyntax>? GetCallableBodyStatements(SyntaxNode declarationNode) =>
        declarationNode switch
        {
            BaseMethodDeclarationSyntax methodDeclaration when methodDeclaration.Body is not null => methodDeclaration.Body.Statements,
            LocalFunctionStatementSyntax localFunction when localFunction.Body is not null => localFunction.Body.Statements,
            AccessorDeclarationSyntax accessorDeclaration when accessorDeclaration.Body is not null => accessorDeclaration.Body.Statements,
            _ => null
        };

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

    private static ITypeSymbol? GetTypeSymbol(ISymbol? symbol) =>
        symbol switch
        {
            ITypeSymbol type => type,
            ILocalSymbol local => local.Type,
            IParameterSymbol parameter => parameter.Type,
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            IMethodSymbol method => method.ReturnType,
            IEventSymbol eventSymbol => eventSymbol.Type,
            _ => null
        };

    private static TypeCompatibilityDescriptor ClassifyTypeCompatibility(
        ITypeSymbol sourceType,
        ITypeSymbol targetType)
    {
        var isIdentity = SymbolEqualityComparer.Default.Equals(sourceType, targetType);
        var targetIsObject = targetType.SpecialType == SpecialType.System_Object;
        var isReference = sourceType.IsReferenceType && targetType.IsReferenceType &&
                          (targetIsObject || InheritsFromOrImplements(sourceType, targetType));
        var isBoxing = sourceType.IsValueType && targetIsObject;
        var isNullable = sourceType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T ||
                         targetType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        var isNumeric = sourceType.SpecialType is >= SpecialType.System_SByte and <= SpecialType.System_Decimal &&
                        targetType.SpecialType is >= SpecialType.System_SByte and <= SpecialType.System_Decimal;
        var exists = isIdentity || isReference || isBoxing || isNumeric;

        return new TypeCompatibilityDescriptor(
            SourceType: sourceType.ToDisplayString(),
            TargetType: targetType.ToDisplayString(),
            IsIdentity: isIdentity,
            Exists: exists,
            IsImplicit: exists,
            IsExplicit: isNumeric && !isIdentity,
            IsReference: isReference,
            IsBoxing: isBoxing,
            IsNumeric: isNumeric,
            IsNullable: isNullable);
    }

    private static bool InheritsFromOrImplements(ITypeSymbol sourceType, ITypeSymbol targetType)
    {
        if (SymbolEqualityComparer.Default.Equals(sourceType, targetType))
        {
            return true;
        }

        if (targetType.TypeKind == TypeKind.Interface &&
            sourceType.AllInterfaces.Any(interfaceType => SymbolEqualityComparer.Default.Equals(interfaceType, targetType)))
        {
            return true;
        }

        for (var current = sourceType.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, targetType))
            {
                return true;
            }
        }

        return false;
    }

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

    private sealed record AnalysisRegion(
        Document? Document,
        SemanticModel? SemanticModel,
        StatementSyntax? FirstStatement,
        StatementSyntax? LastStatement,
        string? FailureReason,
        string? Guidance);
}
