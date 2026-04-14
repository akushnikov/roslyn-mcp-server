using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Application.Workspace;
using RoslynMcpServer.Application.Workspace.Operations;

namespace RoslynMcpServer.UnitTests.Workspace;

public sealed class WorkspaceContextResolverTests
{
    private readonly WorkspaceContextResolver _resolver = new(
        new ResolveWorkspaceContextQueryOperation(
            NullLogger<ResolveWorkspaceContextQueryOperation>.Instance));

    [Fact]
    public async Task ResolveAsync_PrefersExplicitSolutionPath()
    {
        var result = await _resolver.ResolveAsync(new ResolveWorkspaceContextRequest(
            ExplicitSolutionPath: @".\repo\MySolution.sln",
            ConfiguredSolutionPath: @"C:\configured\Configured.sln",
            ClientRoots:
            [
                new WorkspaceRoot("file:///C:/repo", "repo", @"C:\repo")
            ],
            ClientSupportsRoots: true));

        Assert.True(result.IsResolved);
        Assert.NotNull(result.Context);
        Assert.Equal(WorkspaceContextSource.ExplicitSolutionPath, result.Context.Source);
        Assert.EndsWith(@"repo\MySolution.sln", result.Context.SolutionPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_UsesClientRootsWhenSolutionPathIsMissing()
    {
        var result = await _resolver.ResolveAsync(new ResolveWorkspaceContextRequest(
            ExplicitSolutionPath: null,
            ConfiguredSolutionPath: null,
            ClientRoots:
            [
                new WorkspaceRoot("file:///C:/repo", "repo", @"C:\repo")
            ],
            ClientSupportsRoots: true));

        Assert.True(result.IsResolved);
        Assert.NotNull(result.Context);
        Assert.Equal(WorkspaceContextSource.ClientRoots, result.Context.Source);
        Assert.Null(result.Context.SolutionPath);
        Assert.Single(result.Context.Roots);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsGuidanceWhenNoContextIsAvailable()
    {
        var result = await _resolver.ResolveAsync(new ResolveWorkspaceContextRequest(
            ExplicitSolutionPath: null,
            ConfiguredSolutionPath: null,
            ClientRoots: Array.Empty<WorkspaceRoot>(),
            ClientSupportsRoots: false));

        Assert.False(result.IsResolved);
        Assert.Null(result.Context);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("solution path", result.Guidance!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_MapsCanceledResult_ToStableCancellationPayload()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await _resolver.ResolveAsync(
            new ResolveWorkspaceContextRequest(
                ExplicitSolutionPath: @"C:\repo\MySolution.sln",
                ConfiguredSolutionPath: null,
                ClientRoots: Array.Empty<WorkspaceRoot>(),
                ClientSupportsRoots: false),
            cts.Token);

        Assert.False(result.IsResolved);
        Assert.Null(result.Context);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
    }
}
