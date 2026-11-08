using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>Supplies ValidateCode query data from the loaded Roslyn workspace.</summary>
internal sealed class RoslynValidateCodeQueryDataProvider(
    IDiagnosticsQueryDataProvider diagnosticsQueryDataProvider) : IValidateCodeQueryDataProvider
{
    /// <inheritdoc />
    public ValueTask<ValidateCodeResult> ValidateCodeAsync(
        ValidateCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = diagnosticsQueryDataProvider;
        throw new NotImplementedException();
    }
}
