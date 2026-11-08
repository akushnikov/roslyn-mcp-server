using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Application.Analysis.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Analysis;

/// <summary>Projects ValidateCode query outcomes without storing request context.</summary>
public sealed class ValidateCodeQueryService(
    ValidateCodeQueryOperation operation) : IValidateCodeQueryService
{
    /// <inheritdoc />
    public async ValueTask<ValidateCodeResult> ValidateCodeAsync(
        ValidateCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new ValidateCodeResult(
                IsValid: false,
                ErrorCount: 0,
                WarningCount: 0,
                Diagnostics: Array.Empty<AnalysisDiagnosticDescriptor>(),
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new ValidateCodeResult(
                IsValid: false,
                ErrorCount: 0,
                WarningCount: 0,
                Diagnostics: Array.Empty<AnalysisDiagnosticDescriptor>(),
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
