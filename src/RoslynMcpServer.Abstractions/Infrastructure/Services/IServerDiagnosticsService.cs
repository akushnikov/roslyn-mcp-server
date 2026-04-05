using RoslynMcpServer.Abstractions.Infrastructure.Requests;
using RoslynMcpServer.Abstractions.Infrastructure.Results;

namespace RoslynMcpServer.Abstractions.Infrastructure.Services;

/// <summary>
/// Produces a diagnostic report for the current server environment and workspace path.
/// </summary>
public interface IServerDiagnosticsService
{
    /// <summary>
    /// Returns a diagnostic report for the current process and optional solution path.
    /// </summary>
    ValueTask<DiagnoseResult> DiagnoseAsync(
        DiagnoseRequest request,
        CancellationToken cancellationToken = default);
}
