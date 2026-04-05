using Microsoft.Extensions.DependencyInjection;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Abstractions.WorkspaceSync.Requests;
using RoslynMcpServer.Abstractions.WorkspaceSync.Services;
using RoslynMcpServer.Infrastructure.DependencyInjection;

namespace RoslynMcpServer.IntegrationTests;

public sealed class WorkspaceSyncIntegrationTests
{
    [Fact]
    public async Task ExternalTextChanges_AreVisibleThroughSemanticQueries()
    {
        var fixture = await TemporaryFixture.CreateSingleProjectAsync();

        try
        {
            await using var provider = CreateProvider();
            var loader = provider.GetRequiredService<IWorkspaceLoader>();
            var navigationService = provider.GetRequiredService<INavigationService>();
            var analysisService = provider.GetRequiredService<IAnalysisService>();

            await loader.LoadAsync(new LoadSolutionRequest(fixture.SolutionPath, ForceReload: false));

            var documentPath = Path.Combine(fixture.ProjectDirectory, "GreetingService.cs");
            await File.WriteAllTextAsync(
                documentPath,
                """
                namespace SingleProjectSample;

                public sealed class WelcomeService
                {
                    public string Greet(string name) => $"Hi, {name}!";
                }
                """);

            await AwaitConditionAsync(async () =>
            {
                var result = await navigationService.GetSymbolInfoAsync(
                    new GetSymbolInfoRequest(fixture.SolutionPath, documentPath, Line: 3, Column: 21));

                return string.Equals(result.Symbol?.Name, "WelcomeService", StringComparison.Ordinal);
            });

            await File.WriteAllTextAsync(
                documentPath,
                """
                namespace SingleProjectSample;

                public sealed class WelcomeService
                {
                    public string Greet(string name) => MissingGreeting(name);
                }
                """);

            await AwaitConditionAsync(async () =>
            {
                var result = await analysisService.GetDiagnosticsAsync(
                    new GetDiagnosticsRequest(fixture.SolutionPath, documentPath, SeverityFilter: "Error"));

                return result.Diagnostics.Any(static diagnostic => diagnostic.Id == "CS0103");
            });
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task ExternalCrossDocumentChanges_AreVisibleThroughFindReferences()
    {
        var fixture = await TemporaryFixture.CreateMultiProjectAsync();

        try
        {
            await using var provider = CreateProvider();
            var loader = provider.GetRequiredService<IWorkspaceLoader>();
            var navigationService = provider.GetRequiredService<INavigationService>();

            await loader.LoadAsync(new LoadSolutionRequest(fixture.SolutionPath, ForceReload: false));

            var messagePath = Path.Combine(fixture.CoreProjectDirectory!, "Contracts", "Message.cs");
            var formatterPath = Path.Combine(fixture.CoreProjectDirectory!, "Services", "MessageFormatter.cs");
            var appRunnerPath = Path.Combine(fixture.AppProjectDirectory!, "AppRunner.cs");

            await File.WriteAllTextAsync(
                messagePath,
                """
                namespace MultiProjectSample.Core.Contracts;

                public sealed record MessagePayload(string Text);
                """);

            await File.WriteAllTextAsync(
                formatterPath,
                """
                using MultiProjectSample.Core.Contracts;

                namespace MultiProjectSample.Core.Services;

                public sealed class MessageFormatter
                {
                    public string Format(MessagePayload message) => $"[{message.Text}]";
                }
                """);

            await File.WriteAllTextAsync(
                appRunnerPath,
                """
                using MultiProjectSample.Core.Contracts;
                using MultiProjectSample.Core.Services;

                namespace MultiProjectSample.App;

                public sealed class AppRunner
                {
                    private readonly MessageFormatter _formatter = new();

                    public string Run(string text) => _formatter.Format(new MessagePayload(text));
                }
                """);

            await AwaitConditionAsync(async () =>
            {
                var symbolInfo = await navigationService.GetSymbolInfoAsync(
                    new GetSymbolInfoRequest(fixture.SolutionPath, messagePath, Line: 3, Column: 22));

                var references = await navigationService.FindReferencesAsync(
                    new FindReferencesRequest(fixture.SolutionPath, messagePath, Line: 3, Column: 22));

                return string.Equals(symbolInfo.Symbol?.Name, "MessagePayload", StringComparison.Ordinal) &&
                       references.References.Count >= 2;
            });
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task StructuralChanges_AreVisibleThroughProjectStructureAndSymbolSearch()
    {
        var fixture = await TemporaryFixture.CreateSingleProjectAsync();

        try
        {
            await using var provider = CreateProvider();
            var loader = provider.GetRequiredService<IWorkspaceLoader>();
            var projectStructureService = provider.GetRequiredService<IProjectStructureService>();
            var navigationService = provider.GetRequiredService<INavigationService>();
            var analysisService = provider.GetRequiredService<IAnalysisService>();

            await loader.LoadAsync(new LoadSolutionRequest(fixture.SolutionPath, ForceReload: false));

            var createdFilePath = Path.Combine(fixture.ProjectDirectory, "NewFeature.cs");
            await File.WriteAllTextAsync(
                createdFilePath,
                """
                namespace SingleProjectSample;

                public sealed class NewFeature
                {
                }
                """);

            await AwaitConditionAsync(async () =>
            {
                var structure = await projectStructureService.GetProjectStructureAsync(
                    new GetProjectStructureRequest(fixture.SolutionPath, IncludeDocuments: true));

                var hasDocument = structure.Projects
                    .SelectMany(static project => project.Documents)
                    .Any(document => string.Equals(document.FilePath, createdFilePath, StringComparison.OrdinalIgnoreCase));

                if (!hasDocument)
                {
                    return false;
                }

                var symbols = await navigationService.SearchSymbolsAsync(
                    new SearchSymbolsRequest(fixture.SolutionPath, "NewFeature", "Class", MaxResults: 10));

                if (!symbols.Symbols.Any(static symbol => symbol.Name == "NewFeature"))
                {
                    return false;
                }

                var hierarchy = await analysisService.GetTypeHierarchyAsync(
                    new GetTypeHierarchyRequest(fixture.SolutionPath, createdFilePath, Line: 3, Column: 21, Direction: "Both"));

                return string.Equals(hierarchy.Symbol?.Name, "NewFeature", StringComparison.Ordinal);
            });
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task CacheHitLoadSolution_RestartsWorkspaceSynchronization()
    {
        var fixture = await TemporaryFixture.CreateSingleProjectAsync();

        try
        {
            await using var provider = CreateProvider();
            var loader = provider.GetRequiredService<IWorkspaceLoader>();
            var workspaceSyncService = provider.GetRequiredService<IWorkspaceSyncService>();

            await loader.LoadAsync(new LoadSolutionRequest(fixture.SolutionPath, ForceReload: false));
            await workspaceSyncService.StopAsync(new StopWorkspaceSyncRequest(fixture.SolutionPath));

            var cachedLoad = await loader.LoadAsync(new LoadSolutionRequest(fixture.SolutionPath, ForceReload: false));
            var ensureStarted = await workspaceSyncService.EnsureStartedAsync(new EnsureWorkspaceSyncRequest(fixture.SolutionPath));

            Assert.True(cachedLoad.WasAlreadyLoaded);
            Assert.True(ensureStarted.WasAlreadyRunning);
            Assert.False(ensureStarted.WasStarted);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static ServiceProvider CreateProvider() =>
        new ServiceCollection()
            .AddLogging()
            .AddRoslynMcpServer()
            .BuildServiceProvider();

    private static async Task AwaitConditionAsync(
        Func<Task<bool>> condition,
        int timeoutMs = 15000,
        int pollMs = 150)
    {
        var startedAt = DateTime.UtcNow;

        while (DateTime.UtcNow - startedAt < TimeSpan.FromMilliseconds(timeoutMs))
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(pollMs);
        }

        Assert.Fail($"Condition was not satisfied within {timeoutMs} ms.");
    }

    private sealed class TemporaryFixture : IDisposable
    {
        private TemporaryFixture(
            string rootDirectory,
            string solutionPath,
            string projectDirectory,
            string? appProjectDirectory = null,
            string? coreProjectDirectory = null)
        {
            RootDirectory = rootDirectory;
            SolutionPath = solutionPath;
            ProjectDirectory = projectDirectory;
            AppProjectDirectory = appProjectDirectory;
            CoreProjectDirectory = coreProjectDirectory;
        }

        public string RootDirectory { get; }

        public string SolutionPath { get; }

        public string ProjectDirectory { get; }

        public string? AppProjectDirectory { get; }

        public string? CoreProjectDirectory { get; }

        public static Task<TemporaryFixture> CreateSingleProjectAsync()
        {
            var repoRoot = FindRepoRoot();
            var sourceRoot = Path.Combine(repoRoot, "tests", "Fixtures", "SingleProjectSample");
            var targetRoot = CreateTargetRoot();

            CopyDirectory(sourceRoot, targetRoot);

            return Task.FromResult(new TemporaryFixture(
                rootDirectory: targetRoot,
                solutionPath: Path.Combine(targetRoot, "SingleProjectSample.sln"),
                projectDirectory: Path.Combine(targetRoot, "SingleProjectSample")));
        }

        public static Task<TemporaryFixture> CreateMultiProjectAsync()
        {
            var repoRoot = FindRepoRoot();
            var sourceRoot = Path.Combine(repoRoot, "tests", "Fixtures", "MultiProjectSample");
            var targetRoot = CreateTargetRoot();

            CopyDirectory(sourceRoot, targetRoot);

            return Task.FromResult(new TemporaryFixture(
                rootDirectory: targetRoot,
                solutionPath: Path.Combine(targetRoot, "MultiProjectSample.sln"),
                projectDirectory: Path.Combine(targetRoot, "MultiProjectSample.App"),
                appProjectDirectory: Path.Combine(targetRoot, "MultiProjectSample.App"),
                coreProjectDirectory: Path.Combine(targetRoot, "MultiProjectSample.Core")));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootDirectory))
                {
                    Directory.Delete(RootDirectory, recursive: true);
                }
            }
            catch
            {
            }
        }

        private static string CreateTargetRoot() =>
            Path.Combine(
                Path.GetTempPath(),
                "roslyn-mcp-sync-tests",
                Guid.NewGuid().ToString("N"));

        private static void CopyDirectory(string sourceDirectory, string targetDirectory)
        {
            Directory.CreateDirectory(targetDirectory);

            foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, directory);
                Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
            }

            foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, file);
                var destination = Path.Combine(targetDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, overwrite: true);
            }
        }

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
