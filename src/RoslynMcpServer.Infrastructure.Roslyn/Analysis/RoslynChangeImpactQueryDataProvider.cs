using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Infrastructure.Roslyn.Navigation;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>Supplies ChangeImpact query data from the loaded Roslyn workspace.</summary>
internal sealed class RoslynChangeImpactQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynChangeImpactQueryDataProvider> logger) : IChangeImpactQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<AnalyzeChangeImpactResult> AnalyzeChangeImpactAsync(
        AnalyzeChangeImpactRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SymbolDescriptor? resolvedSymbol = null;

        try
        {
            var resolved = await helper.ResolveSymbolAsync(
                request.SolutionPath, request.FilePath, request.Line, request.Column, cancellationToken);
            if (resolved.FailureReason is not null)
            {
                return new AnalyzeChangeImpactResult(null, null, resolved.FailureReason, resolved.Guidance);
            }

            if (resolved.Symbol is null || resolved.Session is null)
            {
                return new AnalyzeChangeImpactResult(null, null,
                    "No symbol was resolved at the requested source position.",
                    "Move the cursor onto a declaration or reference token and try again.");
            }

            resolvedSymbol = SymbolResolutionHelper.MapSymbol(resolved.Symbol);
            var maxResults = Math.Min(request.MaxResults, 200);
            var references = new List<SymbolReferenceDescriptor>();
            var referencedSymbols = await SymbolFinder.FindReferencesAsync(
                resolved.Symbol, resolved.Session.Solution, cancellationToken);
            foreach (var referencedSymbol in referencedSymbols)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
                        break;
                    }
                }

                if (references.Count >= maxResults)
                {
                    break;
                }
            }

            var implementations = await SymbolFinder.FindImplementationsAsync(
                resolved.Symbol, resolved.Session.Solution, cancellationToken: cancellationToken);
            var implementationDescriptors = implementations
                .Select(symbol =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return SymbolResolutionHelper.MapSymbol(symbol);
                })
                .Where(static symbol => symbol is not null)
                .Cast<SymbolDescriptor>()
                .DistinctBy(static symbol => $"{symbol.Definition?.FilePath}:{symbol.Definition?.Line}:{symbol.Definition?.Column}:{symbol.DisplayName}")
                .Take(maxResults)
                .ToArray();

            var callers = new List<CallerDescriptor>();
            if (resolved.Symbol is IMethodSymbol or IPropertySymbol or IEventSymbol)
            {
                var callerInfos = await SymbolFinder.FindCallersAsync(
                    resolved.Symbol, resolved.Session.Solution, cancellationToken);
                foreach (var caller in callerInfos)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (var location in caller.Locations)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
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

            cancellationToken.ThrowIfCancellationRequested();
            var impactedFiles = references.Select(static entry => entry.Location.FilePath)
                .Concat(callers.Select(static entry => entry.Location.FilePath))
                .Concat(implementationDescriptors.Select(static entry => entry.Definition?.FilePath).Where(static path => !string.IsNullOrWhiteSpace(path))!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToArray();

            return new AnalyzeChangeImpactResult(resolvedSymbol,
                new ChangeImpactDescriptor(references.Count, callers.Count, implementationDescriptors.Length,
                    impactedFiles, references, callers, implementationDescriptors), null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to analyze change impact for {FilePath}:{Line}:{Column}",
                request.FilePath, request.Line, request.Column);
            return new AnalyzeChangeImpactResult(resolvedSymbol, null,
                "The server could not compute a semantic impact summary for the resolved symbol.",
                "Retry the request on a source symbol inside the loaded solution.");
        }
    }

    private static async ValueTask<SymbolReferenceDescriptor?> MapReferenceAsync(
        ReferenceLocation location, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var lineSpan = location.Location.GetLineSpan();
        var sourceTree = location.Location.SourceTree;
        var sourceText = sourceTree is null ? null : await sourceTree.GetTextAsync(cancellationToken);
        string? lineText = null;
        if (sourceText is not null && lineSpan.StartLinePosition.Line < sourceText.Lines.Count)
        {
            lineText = sourceText.Lines[lineSpan.StartLinePosition.Line].ToString().Trim();
        }

        return new SymbolReferenceDescriptor(
            new SymbolLocation(location.Document.FilePath, lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1), null, lineText);
    }

    private static async Task<CallerDescriptor?> MapCallerAsync(
        string? callingSymbol, Location location, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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

        return new CallerDescriptor(callingSymbol,
            new SymbolLocation(lineSpan.Path, lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1), lineText);
    }
}
