using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Infrastructure.DependencyInjection;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;

namespace RoslynMcpServer.IntegrationTests;

public sealed class OutgoingCallsQueryIntegrationTests
{
    [Fact]
    public async Task CrossFileCallSite_UsesDeclarationSemanticModelAndDeclarationLocations()
    {
        using var fixture = new TestWorkspace();
        var service = fixture.Provider.GetRequiredService<IOutgoingCallsQueryService>();

        var declaration = await service.GetOutgoingCallsAsync(fixture.Request("Worker.cs", "Run()"));
        var reference = await service.GetOutgoingCallsAsync(fixture.Request("Consumer.cs", "Run()"));

        Assert.Null(declaration.FailureReason);
        Assert.Null(reference.FailureReason);
        Assert.Equal(declaration.Symbol, reference.Symbol);
        Assert.Equal(declaration.Calls, reference.Calls);
        Assert.Equal(new[] { "First", "Second", ".ctor" }, reference.Calls.Select(call => call.Target!.Name));
        Assert.All(reference.Calls, call => Assert.Equal(fixture.PathFor("Worker.cs"), call.Location.FilePath));
        Assert.True(reference.Calls[0].Location.Line < reference.Calls[1].Location.Line);
        Assert.True(reference.Calls[2].Location.Line < reference.Calls[0].Location.Line);
        Assert.Equal(reference.Calls.Count, reference.Calls.DistinctBy(call =>
            (call.Location.FilePath, call.Location.Line, call.Location.Column, call.Target?.DisplayName)).Count());
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(100, 100)]
    [InlineData(500, 200)]
    public async Task RealProvider_PreservesRequestedLimitAndHardCap(int requested, int expected)
    {
        using var fixture = new TestWorkspace();
        var provider = fixture.Provider.GetRequiredService<IOutgoingCallsQueryDataProvider>();

        var result = await provider.GetOutgoingCallsAsync(fixture.Request("Worker.cs", "Many()") with { MaxResults = requested });

        Assert.Null(result.FailureReason);
        Assert.Equal(expected, result.Calls.Count);
        Assert.All(result.Calls, call => Assert.Equal("First", call.Target?.Name));
        Assert.Equal(result.Calls.OrderBy(call => call.Location.Line).ThenBy(call => call.Location.Column), result.Calls);
        Assert.Equal(expected, result.Calls.Select(call => call.Location).Distinct().Count());
    }

    [Theory]
    [InlineData("Worker.cs", "Worker", "The resolved symbol does not have an analyzable callable body.", "Worker")]
    [InlineData("Consumer.cs", "ToString()", "The resolved callable symbol does not have source syntax available.", "ToString")]
    public async Task UnsupportedOrMetadataSymbol_PreservesContextAndFailureWording(string file, string marker, string failure, string symbol)
    {
        using var fixture = new TestWorkspace();

        var result = await fixture.Provider.GetRequiredService<IOutgoingCallsQueryService>()
            .GetOutgoingCallsAsync(fixture.Request(file, marker));

        Assert.Equal(failure, result.FailureReason);
        Assert.Equal(symbol, result.Symbol?.Name);
        Assert.Empty(result.Calls);
        Assert.False(string.IsNullOrWhiteSpace(result.Guidance));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealProviderCancellation_PropagatesOrProjectsWithoutPartialResults(bool throughService)
    {
        using var fixture = new TestWorkspace();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var request = fixture.Request("Worker.cs", "Run()");

        if (throughService)
        {
            var result = await fixture.Provider.GetRequiredService<IOutgoingCallsQueryService>()
                .GetOutgoingCallsAsync(request, cancellation.Token);
            Assert.Equal("The operation was canceled.", result.FailureReason);
            Assert.Null(result.Symbol);
            Assert.Empty(result.Calls);
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Provider
                .GetRequiredService<IOutgoingCallsQueryDataProvider>().GetOutgoingCallsAsync(request, cancellation.Token).AsTask());
        }
    }

    private sealed class TestWorkspace : IDisposable
    {
        private static readonly string Worker = """
            namespace Sample;
            public class Worker
            {
                public void Run()
                {
                    var value = new Worker();
                    First();
                    Second();
                }
                public void First() { }
                public void Second() { }
                public void Many() {
            """ + "\n" + string.Join("\n", Enumerable.Repeat("First();", 205)) + "\n} }";
        private const string Consumer = """
            namespace Sample;
            public class Consumer
            {
                public void Use(Worker worker) { worker.Run(); worker.ToString(); }
            }
            """;
        private readonly AdhocWorkspace _workspace = new();
        private readonly string _root = Path.Combine(Path.GetTempPath(), "outgoing-query-tests", Guid.NewGuid().ToString("N"));

        public ServiceProvider Provider { get; }

        public TestWorkspace()
        {
            var project = _workspace.AddProject("Outgoing", LanguageNames.CSharp);
            var solution = project.Solution
                .WithProjectCompilationOptions(project.Id, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddMetadataReference(project.Id, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddDocument(DocumentId.CreateNewId(project.Id), "Worker.cs", SourceText.From(Worker), filePath: PathFor("Worker.cs"))
                .AddDocument(DocumentId.CreateNewId(project.Id), "Consumer.cs", SourceText.From(Consumer), filePath: PathFor("Consumer.cs"));
            Provider = new ServiceCollection().AddLogging().AddRoslynMcpServer()
                .AddSingleton<IWorkspaceSessionProvider>(new SessionProvider(new RoslynWorkspaceSession(null!, _workspace, solution)))
                .BuildServiceProvider();
        }

        public string PathFor(string file) => Path.Combine(_root, file);

        public GetOutgoingCallsRequest Request(string file, string marker)
        {
            var source = file == "Worker.cs" ? Worker : Consumer;
            var offset = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(offset >= 0, $"Marker '{marker}' must exist in {file}.");
            var position = SourceText.From(source).Lines.GetLinePosition(offset);
            return new GetOutgoingCallsRequest(PathFor("Outgoing.csproj"), PathFor(file), position.Line + 1, position.Character + 1);
        }

        public void Dispose()
        {
            Provider.Dispose();
            _workspace.Dispose();
        }
    }

    private sealed class SessionProvider(RoslynWorkspaceSession session) : IWorkspaceSessionProvider
    {
        public ValueTask<RoslynWorkspaceSession?> GetReadySessionAsync(string solutionPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<RoslynWorkspaceSession?>(session);
        }

        public ValueTask<LoadedWorkspaceSnapshot?> GetReadySnapshotAsync(string solutionPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<LoadedWorkspaceSnapshot?>(null);
        }
    }
}
