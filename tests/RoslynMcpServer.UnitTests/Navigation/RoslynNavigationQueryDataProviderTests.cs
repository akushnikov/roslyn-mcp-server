using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Application.Navigation;
using RoslynMcpServer.Application.Navigation.Operations;
using RoslynMcpServer.Infrastructure.Roslyn.Navigation;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class RoslynNavigationQueryDataProviderTests
{
    [Fact]
    public async Task GoToDefinitionAsync_PreservesMetadataSymbol_ThroughPipeline()
    {
        const string source = "public class Sample { public string Value => string.Empty; }";
        using var workspace = new AdhocWorkspace();
        var filePath = Path.GetFullPath("NavigationQuerySample.cs");
        var session = CreateSession(workspace, source, filePath);
        var provider = new RoslynGoToDefinitionQueryDataProvider(
            new SymbolResolutionHelper(new TestWorkspaceSessionProvider(session)),
            NullLogger<RoslynGoToDefinitionQueryDataProvider>.Instance);
        var service = new GoToDefinitionQueryService(
            new GoToDefinitionQueryOperation(provider, NullLogger<GoToDefinitionQueryOperation>.Instance));

        var result = await service.GoToDefinitionAsync(
            new GoToDefinitionRequest(session.Snapshot.Workspace.SolutionPath, filePath, 1, source.IndexOf("Empty", StringComparison.Ordinal) + 1));

        Assert.NotNull(result.Symbol);
        Assert.Equal("Empty", result.Symbol.Name);
        Assert.Equal("string", result.Symbol.ContainerName);
        Assert.Empty(result.Definitions);
        Assert.Equal("No source definition was found for the resolved symbol.", result.FailureReason);
        Assert.Equal("The symbol may come from metadata or generated code outside the loaded solution.", result.Guidance);
    }

    [Fact]
    public async Task FindReferencesAsync_PreservesResolvedSymbol_WhenFailureOccursAfterResolution()
    {
        const string source = "public class Sample { }";
        using var workspace = new AdhocWorkspace();
        var filePath = Path.GetFullPath("NavigationQuerySample.cs");
        var session = CreateSession(workspace, source, filePath);
        var provider = new RoslynFindReferencesQueryDataProvider(
            new SymbolResolutionHelper(new TestWorkspaceSessionProvider(session)),
            NullLogger<RoslynFindReferencesQueryDataProvider>.Instance);

        // Direct provider access bypasses operation validation to force a list-capacity fault
        // after symbol resolution and Roslyn search, without introducing a production test seam.
        var result = await provider.FindReferencesAsync(
            new FindReferencesRequest(session.Snapshot.Workspace.SolutionPath, filePath, 1, 14, MaxResults: -1));

        Assert.NotNull(result.Symbol);
        Assert.Equal("Sample", result.Symbol.Name);
        Assert.Empty(result.References);
        Assert.Equal("The server could not compute references for the resolved symbol.", result.FailureReason);
        Assert.Equal("Retry the request and confirm the file position resolves to a source symbol.", result.Guidance);
    }

    private static RoslynWorkspaceSession CreateSession(AdhocWorkspace workspace, string source, string filePath)
    {
        var document = workspace.AddProject("NavigationQuerySample", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddDocument("NavigationQuerySample.cs", SourceText.From(source), filePath: filePath);
        var snapshot = new LoadedWorkspaceSnapshot(
            new WorkspaceDescriptor(Path.GetFullPath("NavigationQuerySample.sln"), "NavigationQuerySample", DateTimeOffset.UtcNow, 1, 1),
            Array.Empty<ProjectStructureDescriptor>());

        return new RoslynWorkspaceSession(snapshot, workspace, document.Project.Solution);
    }

    private sealed class TestWorkspaceSessionProvider(RoslynWorkspaceSession session) : IWorkspaceSessionProvider
    {
        public ValueTask<RoslynWorkspaceSession?> GetReadySessionAsync(string solutionPath, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<RoslynWorkspaceSession?>(session);

        public ValueTask<LoadedWorkspaceSnapshot?> GetReadySnapshotAsync(string solutionPath, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<LoadedWorkspaceSnapshot?>(session.Snapshot);
    }
}
