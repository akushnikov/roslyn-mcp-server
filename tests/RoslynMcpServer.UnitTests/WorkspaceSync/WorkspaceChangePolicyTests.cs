using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.WorkspaceSync.Models;
using RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

namespace RoslynMcpServer.UnitTests.WorkspaceSync;

public sealed class WorkspaceChangePolicyTests
{
    private readonly WorkspaceChangePolicy _policy = new(NullLogger<WorkspaceChangePolicy>.Instance);

    [Fact]
    public async Task EvaluateAsync_ReloadsSolution_ForRenameOfCompileItem()
    {
        var result = await _policy.EvaluateAsync(
            @"C:\repo\Sample.sln",
            new[]
            {
                new WorkspaceEventEnvelope(
                    WorkspacePath: @"C:\repo\Sample.sln",
                    FilePath: @"C:\repo\src\NewFile.cs",
                    OldFilePath: @"C:\repo\src\OldFile.cs",
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    Origin: ChangeOrigin.FileSystem,
                    Kind: WorkspaceFileEventKind.Renamed)
            });

        Assert.True(result.ReloadSolution);
        Assert.False(result.ReconcileOnly);
    }

    [Fact]
    public async Task EvaluateAsync_DeduplicatesDuplicateChangedEvents_IntoSinglePatch()
    {
        var now = DateTimeOffset.UtcNow;
        var result = await _policy.EvaluateAsync(
            @"C:\repo\Sample.sln",
            new[]
            {
                new WorkspaceEventEnvelope(@"C:\repo\Sample.sln", @"C:\repo\src\File.cs", null, now, ChangeOrigin.FileSystem, WorkspaceFileEventKind.Changed),
                new WorkspaceEventEnvelope(@"C:\repo\Sample.sln", @"C:\repo\src\File.cs", null, now.AddMilliseconds(10), ChangeOrigin.FileSystem, WorkspaceFileEventKind.Changed)
            });

        Assert.False(result.ReloadSolution);
        Assert.Single(result.PatchDocuments);
        Assert.Equal(@"C:\repo\src\File.cs", result.PatchDocuments[0].FilePath);
    }

    [Fact]
    public async Task EvaluateAsync_EscalatesLargePatchStorm_ToSolutionReload()
    {
        var events = Enumerable.Range(0, 70)
            .Select(index => new WorkspaceEventEnvelope(
                WorkspacePath: @"C:\repo\Sample.sln",
                FilePath: $@"C:\repo\src\File{index}.cs",
                OldFilePath: null,
                OccurredAtUtc: DateTimeOffset.UtcNow.AddMilliseconds(index),
                Origin: ChangeOrigin.FileSystem,
                Kind: WorkspaceFileEventKind.Changed))
            .ToArray();

        var result = await _policy.EvaluateAsync(@"C:\repo\Sample.sln", events);

        Assert.True(result.ReloadSolution);
        Assert.Empty(result.ReloadProjects);
    }

    [Fact]
    public async Task EvaluateAsync_ReloadsSolution_ForDirectoryBuildProps()
    {
        var result = await _policy.EvaluateAsync(
            @"C:\repo\Sample.sln",
            new[]
            {
                new WorkspaceEventEnvelope(
                    WorkspacePath: @"C:\repo\Sample.sln",
                    FilePath: @"C:\repo\Directory.Build.props",
                    OldFilePath: null,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    Origin: ChangeOrigin.FileSystem,
                    Kind: WorkspaceFileEventKind.Changed)
            });

        Assert.True(result.ReloadSolution);
    }
}
