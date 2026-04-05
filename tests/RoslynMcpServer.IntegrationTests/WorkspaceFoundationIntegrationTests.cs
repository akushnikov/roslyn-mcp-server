using Microsoft.Extensions.DependencyInjection;
using RoslynMcpServer.Abstractions.Infrastructure.Requests;
using RoslynMcpServer.Abstractions.Infrastructure.Services;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Infrastructure.DependencyInjection;

namespace RoslynMcpServer.IntegrationTests;

public sealed class WorkspaceFoundationIntegrationTests
{
    [Fact]
    public async Task DiagnoseAsync_WithoutSolutionPath_ReturnsEnvironmentSummary()
    {
        await using var provider = CreateProvider();
        var diagnosticsService = provider.GetRequiredService<IServerDiagnosticsService>();

        var result = await diagnosticsService.DiagnoseAsync(new DiagnoseRequest(null, Verbose: false));

        Assert.NotNull(result.Server);
        Assert.NotNull(result.Environment);
        Assert.Null(result.Workspace);
        Assert.False(string.IsNullOrWhiteSpace(result.Environment.MsBuildLocatorStatus));
    }

    [Fact]
    public async Task LoadSolutionAsync_LoadsFixtureIntoCache_AndSupportsReloadScenarios()
    {
        await using var provider = CreateProvider();
        var loader = provider.GetRequiredService<IWorkspaceLoader>();
        var stateService = provider.GetRequiredService<IWorkspaceStateService>();

        var solutionPath = TestPaths.SingleProjectSolutionPath;

        var firstLoad = await loader.LoadAsync(new LoadSolutionRequest(solutionPath, ForceReload: false));
        Assert.NotNull(firstLoad.Workspace);
        Assert.False(firstLoad.WasAlreadyLoaded);
        Assert.False(firstLoad.WasReloaded);
        Assert.Equal(1, firstLoad.Workspace!.ProjectCount);

        var secondLoad = await loader.LoadAsync(new LoadSolutionRequest(solutionPath, ForceReload: false));
        Assert.True(secondLoad.WasAlreadyLoaded);
        Assert.NotNull(secondLoad.Workspace);

        var reloaded = await loader.LoadAsync(new LoadSolutionRequest(solutionPath, ForceReload: true));
        Assert.True(reloaded.WasReloaded);
        Assert.NotNull(reloaded.Workspace);

        var state = await stateService.GetStateAsync(new GetWorkspaceStateRequest(solutionPath));
        Assert.True(state.HasLoadedSolution);
        Assert.NotNull(state.Workspace);
        Assert.Contains(state.CachedSolutions, workspace => string.Equals(workspace.SolutionPath, solutionPath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetProjectStructureAsync_ReturnsProjectsAndDocuments_ForLoadedSolution()
    {
        await using var provider = CreateProvider();
        var loader = provider.GetRequiredService<IWorkspaceLoader>();
        var projectStructureService = provider.GetRequiredService<IProjectStructureService>();

        await loader.LoadAsync(new LoadSolutionRequest(TestPaths.MultiProjectSolutionPath, ForceReload: false));
        var result = await projectStructureService.GetProjectStructureAsync(
            new GetProjectStructureRequest(TestPaths.MultiProjectSolutionPath, IncludeDocuments: true));

        Assert.NotNull(result.Workspace);
        Assert.Equal(2, result.Projects.Count);
        Assert.Contains(result.Projects, project => project.Documents.Count >= 2);
        Assert.Contains(result.Projects, project => project.ProjectReferences.Count == 1);
    }

    [Fact]
    public async Task LoadSolutionAsync_ReturnsControlledFailure_ForInvalidAndUnsupportedPaths()
    {
        await using var provider = CreateProvider();
        var loader = provider.GetRequiredService<IWorkspaceLoader>();

        var invalidResult = await loader.LoadAsync(new LoadSolutionRequest(Path.Combine(TestPaths.RepoRoot, "does-not-exist.sln"), ForceReload: false));
        Assert.NotNull(invalidResult.FailureReason);
        Assert.Contains(invalidResult.Diagnostics, diagnostic => diagnostic.Code == "PathNotFound");

        var unsupportedResult = await loader.LoadAsync(new LoadSolutionRequest(TestPaths.UnsupportedPath, ForceReload: false));
        Assert.NotNull(unsupportedResult.FailureReason);
        Assert.Contains(unsupportedResult.Diagnostics, diagnostic => diagnostic.Code == "UnsupportedWorkspacePath");
    }

    private static ServiceProvider CreateProvider() =>
        new ServiceCollection()
            .AddLogging()
            .AddRoslynMcpServer()
            .BuildServiceProvider();

    private static class TestPaths
    {
        public static string RepoRoot { get; } = FindRepoRoot();

        public static string SingleProjectSolutionPath { get; } =
            Path.Combine(RepoRoot, "tests", "Fixtures", "SingleProjectSample", "SingleProjectSample.sln");

        public static string MultiProjectSolutionPath { get; } =
            Path.Combine(RepoRoot, "tests", "Fixtures", "MultiProjectSample", "MultiProjectSample.sln");

        public static string UnsupportedPath { get; } =
            Path.Combine(RepoRoot, "tests", "Fixtures", "unsupported.txt");

        private static string FindRepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "roslyn-mcp-server.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the repository root.");
        }
    }
}
