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

public sealed class RoslynSymbolInfoQueryDataProviderTests
{
    [Fact]
    public async Task GetSymbolInfoAsync_DeclarationAndReference_PreserveDescriptor()
    {
        const string source = "public class Sample { public Sample Value => this; }";
        using var workspace = new AdhocWorkspace();
        var session = CreateSession(workspace, source);
        var service = CreateService(new TestSessionProvider(session));
        var declaration = await service.GetSymbolInfoAsync(Request(session, source.IndexOf("Sample", StringComparison.Ordinal) + 1));
        var reference = await service.GetSymbolInfoAsync(Request(session, source.LastIndexOf("Sample", StringComparison.Ordinal) + 1));

        Assert.Null(declaration.FailureReason);
        Assert.Null(declaration.Guidance);
        Assert.NotNull(declaration.Symbol);
        Assert.Equal("Sample", declaration.Symbol.Name);
        Assert.Equal("Class", declaration.Symbol.Kind);
        Assert.Equal("Sample", declaration.Symbol.DisplayName);
        Assert.NotNull(declaration.Symbol.Definition);
        Assert.Equal(FilePath, declaration.Symbol.Definition.FilePath);
        Assert.Equal(1, declaration.Symbol.Definition.Line);
        Assert.Equal(14, declaration.Symbol.Definition.Column);
        Assert.Equal(declaration.Symbol, reference.Symbol);
        Assert.Null(reference.FailureReason);
    }

    [Fact]
    public async Task GetSymbolInfoAsync_Record_PreservesKind()
    {
        const string source = "public record Sample { }";
        using var workspace = new AdhocWorkspace();
        var session = CreateSession(workspace, source);
        var result = await CreateService(new TestSessionProvider(session))
            .GetSymbolInfoAsync(Request(session, source.IndexOf("Sample", StringComparison.Ordinal) + 1));

        Assert.Null(result.FailureReason);
        Assert.NotNull(result.Symbol);
        Assert.Equal("Sample", result.Symbol.Name);
        Assert.Equal("Record", result.Symbol.Kind);
    }

    private static string FilePath => Path.GetFullPath("SymbolInfoSample.cs");

    private static GetSymbolInfoRequest Request(RoslynWorkspaceSession session, int column) =>
        new(session.Snapshot.Workspace.SolutionPath, FilePath, 1, column);

    private static SymbolInfoQueryService CreateService(IWorkspaceSessionProvider sessions) =>
        new(new SymbolInfoQueryOperation(
            new RoslynSymbolInfoQueryDataProvider(
                new SymbolResolutionHelper(sessions),
                NullLogger<RoslynSymbolInfoQueryDataProvider>.Instance),
            NullLogger<SymbolInfoQueryOperation>.Instance));

    private static RoslynWorkspaceSession CreateSession(AdhocWorkspace workspace, string source)
    {
        var document = workspace.AddProject("SymbolInfoSample", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddDocument("SymbolInfoSample.cs", SourceText.From(source), filePath: FilePath);
        var snapshot = new LoadedWorkspaceSnapshot(
            new WorkspaceDescriptor(Path.GetFullPath("SymbolInfoSample.sln"), "SymbolInfoSample", DateTimeOffset.UtcNow, 1, 1),
            Array.Empty<ProjectStructureDescriptor>());
        return new RoslynWorkspaceSession(snapshot, workspace, document.Project.Solution);
    }

    private sealed class TestSessionProvider(RoslynWorkspaceSession? session) : IWorkspaceSessionProvider
    {
        public ValueTask<RoslynWorkspaceSession?> GetReadySessionAsync(string solutionPath, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(session);

        public ValueTask<LoadedWorkspaceSnapshot?> GetReadySnapshotAsync(string solutionPath, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(session?.Snapshot);
    }
}
