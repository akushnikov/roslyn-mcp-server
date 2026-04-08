using OneOf;
using OneOf.Types;
using RoslynMcpServer.Abstractions.CommandPipeline.Models;
using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Application.CommandPipeline;

namespace RoslynMcpServer.Application.Workspace.Operations;

/// <summary>
/// Executes the load-solution stateful command through the shared command pipeline.
/// </summary>
public sealed class LoadSolutionCommandOperation(
    IWorkspaceLoader workspaceLoader,
    IWorkspaceCache workspaceCache) : CommandOperationBase<LoadSolutionCommandRequest, LoadSolutionCommandResult, CommandError>
{
    protected override async ValueTask<OneOf<Success<LoadSolutionCommandResult>, Error<CommandError>, Canceled>> ExecuteCoreAsync(
        LoadSolutionCommandRequest request,
        CancellationToken cancellationToken)
    {
        var targetPath = request.LoadRequest.SolutionPath;
        var normalizedPath = TryNormalizePath(targetPath);

        var beforeSnapshot = await TryGetSnapshotAsync(targetPath, cancellationToken);
        var beforeVersion = GetWorkspaceVersion(beforeSnapshot?.Workspace.LoadedAtUtc);

        var result = await workspaceLoader.LoadAsync(
            request.LoadRequest,
            request.Progress,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(result.FailureReason))
        {
            return new Error<CommandError>(
                new CommandError(result.FailureReason, result.Guidance));
        }

        var afterVersion = GetWorkspaceVersion(result.Workspace?.LoadedAtUtc);
        var changed = !result.WasAlreadyLoaded || result.WasReloaded;

        return new Success<LoadSolutionCommandResult>(
            new LoadSolutionCommandResult(
                result,
                new CommandEffects(
                    Changed: changed,
                    WorkspaceVersionBefore: beforeVersion,
                    WorkspaceVersionAfter: afterVersion,
                    NormalizedSolutionPath: normalizedPath)));
    }

    protected override Error<CommandError>? Validate(LoadSolutionCommandRequest request)
    {
        if (request.LoadRequest is null)
        {
            return new Error<CommandError>(
                new CommandError(
                    "A load request is required.",
                    "Pass a non-null load request containing solutionPath and forceReload."));
        }

        if (string.IsNullOrWhiteSpace(request.LoadRequest.SolutionPath))
        {
            return new Error<CommandError>(
                new CommandError(
                    "A non-empty solutionPath is required.",
                    "Pass an absolute path to a .sln, .slnx, or .csproj file."));
        }

        return null;
    }

    protected override Error<CommandError> MapUnhandledError(
        LoadSolutionCommandRequest request,
        Exception exception)
    {
        return new Error<CommandError>(
            new CommandError(
                "The server could not load the requested workspace.",
                "Retry the request and verify MSBuild environment readiness."));
    }

    private async ValueTask<LoadedWorkspaceSnapshot?> TryGetSnapshotAsync(
        string solutionPath,
        CancellationToken cancellationToken) =>
        await workspaceCache.GetAsync(solutionPath, cancellationToken);

    private static string? TryNormalizePath(string rawPath)
    {
        try
        {
            return Path.GetFullPath(rawPath);
        }
        catch
        {
            return rawPath;
        }
    }

    private static long? GetWorkspaceVersion(DateTimeOffset? loadedAtUtc) =>
        loadedAtUtc?.ToUnixTimeMilliseconds();
}
