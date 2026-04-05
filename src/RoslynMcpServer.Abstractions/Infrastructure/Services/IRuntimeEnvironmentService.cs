using RoslynMcpServer.Abstractions.Infrastructure.Models;

namespace RoslynMcpServer.Abstractions.Infrastructure.Services;

/// <summary>
/// Provides process and workspace probing information for diagnostics.
/// </summary>
public interface IRuntimeEnvironmentService
{
    /// <summary>
    /// Returns a description of the current runtime environment.
    /// </summary>
    ValueTask<EnvironmentDescriptor> GetEnvironmentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Probes a solution or project path without mutating the shared workspace cache.
    /// </summary>
    ValueTask<WorkspaceHealthDescriptor> ProbeWorkspaceAsync(
        string solutionPath,
        CancellationToken cancellationToken = default);
}
