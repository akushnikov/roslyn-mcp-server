using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>Supplies ValidateCode query data from the loaded Roslyn workspace.</summary>
internal sealed class RoslynValidateCodeQueryDataProvider(
    IDiagnosticsQueryDataProvider diagnosticsQueryDataProvider) : IValidateCodeQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<ValidateCodeResult> ValidateCodeAsync(
        ValidateCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = await diagnosticsQueryDataProvider.GetDiagnosticsAsync(
            new GetDiagnosticsRequest(request.SolutionPath, request.FilePath, "All"),
            cancellationToken);

        if (diagnostics.FailureReason is not null)
        {
            return new ValidateCodeResult(
                false, 0, 0, Array.Empty<AnalysisDiagnosticDescriptor>(),
                diagnostics.FailureReason, diagnostics.Guidance);
        }

        var errorCount = diagnostics.Diagnostics.Count(static diagnostic =>
            diagnostic.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase));
        var warningCount = diagnostics.Diagnostics.Count(static diagnostic =>
            diagnostic.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase));

        return new ValidateCodeResult(
            IsValid: errorCount == 0,
            ErrorCount: errorCount,
            WarningCount: warningCount,
            Diagnostics: diagnostics.Diagnostics,
            FailureReason: null,
            Guidance: errorCount == 0 ? null : "Resolve compiler errors before applying additional semantic edits.");
    }
}
