using Microsoft.CodeAnalysis;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Infrastructure.Roslyn.Navigation;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>
/// Supplies compiler diagnostics from synchronized workspace sessions while preserving file and severity filters.
/// </summary>
internal sealed class RoslynDiagnosticsQueryDataProvider(
    IWorkspaceSessionProvider workspaceSessionProvider) : IDiagnosticsQueryDataProvider
{
    /// <inheritdoc />
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
            : Path.GetFullPath(request.FilePath);

        if (normalizedFilePath is not null && SymbolResolutionHelper.FindDocument(session, normalizedFilePath) is null)
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
}