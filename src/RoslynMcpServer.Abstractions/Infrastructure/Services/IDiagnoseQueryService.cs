using RoslynMcpServer.Abstractions.Infrastructure.Requests;
using RoslynMcpServer.Abstractions.Infrastructure.Results;

namespace RoslynMcpServer.Abstractions.Infrastructure.Services;

/// <summary>
/// Retrieves server environment and optional workspace diagnostics through the query pipeline.
/// </summary>
public interface IDiagnoseQueryService
{
    /// <summary>Returns a diagnostic snapshot with an optional workspace probe.</summary>
    /// <param name="request">The optional solution path and diagnostic verbosity.</param>
    /// <param name="cancellationToken">Cancels diagnostic collection.</param>
    /// <returns>The server, environment and optional workspace diagnostic snapshot.</returns>
    ValueTask<DiagnoseResult> DiagnoseAsync(
        DiagnoseRequest request,
        CancellationToken cancellationToken = default);
}
