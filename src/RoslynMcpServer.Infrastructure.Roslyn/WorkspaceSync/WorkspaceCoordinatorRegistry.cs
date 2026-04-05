using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.WorkspaceSync.Models;
using RoslynMcpServer.Abstractions.WorkspaceSync.Requests;
using RoslynMcpServer.Abstractions.WorkspaceSync.Results;
using RoslynMcpServer.Abstractions.WorkspaceSync.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Owns workspace coordinator instances and exposes the synchronization subsystem through abstraction-layer services.
/// </summary>
internal sealed class WorkspaceCoordinatorRegistry(
    IServiceProvider serviceProvider,
    ILogger<WorkspaceCoordinatorRegistry> logger) : IWorkspaceSyncService, IWorkspaceMutationNotifier
{
    private readonly ConcurrentDictionary<string, WorkspaceCoordinator> _coordinators = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Ensures that a coordinator exists and is running for the requested workspace.
    /// </summary>
    public async ValueTask<EnsureWorkspaceSyncResult> EnsureStartedAsync(
        EnsureWorkspaceSyncRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.WorkspacePath))
        {
            return new EnsureWorkspaceSyncResult(
                Workspace: null,
                WasStarted: false,
                WasAlreadyRunning: false,
                FailureReason: "A workspacePath is required.",
                Guidance: "Pass the absolute solution or project path for the loaded workspace.");
        }

        var normalizedPath = NormalizePath(request.WorkspacePath);
        var coordinator = _coordinators.GetOrAdd(normalizedPath, CreateCoordinator);
        var wasStarted = await coordinator.StartAsync(cancellationToken);

        return new EnsureWorkspaceSyncResult(
            Workspace: coordinator.Describe(),
            WasStarted: wasStarted,
            WasAlreadyRunning: !wasStarted,
            FailureReason: null,
            Guidance: null);
    }

    /// <summary>
    /// Stops and removes the coordinator for the requested workspace when present.
    /// </summary>
    public async ValueTask StopAsync(
        StopWorkspaceSyncRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.WorkspacePath))
        {
            return;
        }

        var normalizedPath = NormalizePath(request.WorkspacePath);
        if (_coordinators.TryRemove(normalizedPath, out var coordinator))
        {
            await coordinator.StopAsync(cancellationToken);
        }
    }

    internal async ValueTask WaitForQuiescenceAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        cancellationToken.ThrowIfCancellationRequested();

        if (_coordinators.TryGetValue(NormalizePath(workspacePath), out var coordinator))
        {
            await coordinator.WaitForQuiescenceAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Registers expected external changes produced by a Roslyn-originated mutation.
    /// </summary>
    public async ValueTask<NotifyWorkspaceMutationResult> NotifyAsync(
        NotifyWorkspaceMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.WorkspacePath))
        {
            return new NotifyWorkspaceMutationResult(
                Accepted: false,
                RegisteredChangeCount: 0,
                FailureReason: "A workspacePath is required.",
                Guidance: "Pass the absolute solution or project path for the loaded workspace.");
        }

        if (!_coordinators.TryGetValue(NormalizePath(request.WorkspacePath), out var coordinator))
        {
            return new NotifyWorkspaceMutationResult(
                Accepted: false,
                RegisteredChangeCount: 0,
                FailureReason: "Live synchronization is not running for the requested workspace.",
                Guidance: "Start synchronization before registering Roslyn-originated mutations.");
        }

        var count = await coordinator.NotifyRoslynMutationAsync(request.ExpectedExternalChanges, cancellationToken);
        logger.LogDebug(
            "Registered {ChangeCount} expected external change(s) for workspace {WorkspacePath}",
            count,
            request.WorkspacePath);

        return new NotifyWorkspaceMutationResult(
            Accepted: true,
            RegisteredChangeCount: count,
            FailureReason: null,
            Guidance: null);
    }

    private WorkspaceCoordinator CreateCoordinator(string workspacePath)
    {
        logger.LogDebug("Creating workspace sync coordinator for {WorkspacePath}", workspacePath);
        return ActivatorUtilities.CreateInstance<WorkspaceCoordinator>(serviceProvider, workspacePath);
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);
}
