using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>
/// Supplies compiler diagnostics from Roslyn workspace sessions.
/// </summary>
internal sealed class RoslynDiagnosticsQueryDataProvider(
    IWorkspaceSessionProvider workspaceSessionProvider) : IDiagnosticsQueryDataProvider
{
    /// <inheritdoc />
    public ValueTask<GetDiagnosticsResult> GetDiagnosticsAsync(
        GetDiagnosticsRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
