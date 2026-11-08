using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Infrastructure.DependencyInjection;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;

namespace RoslynMcpServer.IntegrationTests;

public sealed class MethodAnalysisQueryIntegrationTests
{
    [Fact]
    public async Task CallSiteInAnotherFile_UsesDeclarationModelForFlowAndOutgoingCalls()
    {
        using var fixture = Fixture.Create();
        var version = fixture.Solution.Version;
        var before = await fixture.TextsAsync();
        var declaration = await fixture.AnalyzeAsync("Decl.cs", "Analyze(int input)");
        var callSite = await fixture.AnalyzeAsync("Caller.cs", "Analyze(4)");

        Assert.Null(declaration.FailureReason);
        Assert.Null(callSite.FailureReason);
        Assert.NotNull(declaration.Signature);
        Assert.Equal(declaration.Signature.DisplayName, callSite.Signature?.DisplayName);
        Assert.Equal(fixture.PathFor("Decl.cs"), callSite.Signature?.Definition?.FilePath);
        Assert.NotNull(declaration.DataFlow);
        Assert.NotNull(callSite.DataFlow);
        Assert.True(callSite.DataFlow.Succeeded);
        Assert.Equal(declaration.DataFlow.VariablesDeclared, callSite.DataFlow.VariablesDeclared);
        Assert.Equal(declaration.DataFlow.DataFlowsIn, callSite.DataFlow.DataFlowsIn);
        Assert.Equal(declaration.DataFlow.WrittenInside, callSite.DataFlow.WrittenInside);
        Assert.Contains("value", callSite.DataFlow.VariablesDeclared);
        Assert.Contains("input", callSite.DataFlow.ReadInside);
        Assert.NotNull(callSite.ControlFlow);
        Assert.True(callSite.ControlFlow.Succeeded);
        Assert.NotNull(declaration.ControlFlow);
        Assert.Equal(declaration.ControlFlow.StartPointIsReachable, callSite.ControlFlow.StartPointIsReachable);
        Assert.Equal(declaration.ControlFlow.EndPointIsReachable, callSite.ControlFlow.EndPointIsReachable);
        Assert.Equal(declaration.ControlFlow.ReturnStatements, callSite.ControlFlow.ReturnStatements);
        Assert.Equal(declaration.ControlFlow.EntryPoints, callSite.ControlFlow.EntryPoints);
        Assert.Equal(declaration.ControlFlow.ExitPoints, callSite.ControlFlow.ExitPoints);
        Assert.Equal(declaration.OutgoingCalls, callSite.OutgoingCalls);
        Assert.Equal(new[] { "Helper", ".ctor" }, callSite.OutgoingCalls.Select(static edge => edge.Target!.Name));
        Assert.All(callSite.OutgoingCalls, edge => Assert.Equal(fixture.PathFor("Decl.cs"), edge.Location.FilePath));
        Assert.Equal(callSite.OutgoingCalls.Count, callSite.OutgoingCalls.Select(static edge => (edge.Location, edge.Target?.DisplayName)).Distinct().Count());
        Assert.Equal(version, fixture.Solution.Version);
        Assert.Equal(before, await fixture.TextsAsync());
    }

    [Fact]
    public async Task OutgoingCalls_PreserveInvocationBeforeCreationOrderAndRequestedCap()
    {
        using var fixture = Fixture.Create();
        var result = await fixture.AnalyzeAsync("Caller.cs", "Analyze(4)", 1);
        Assert.Null(result.FailureReason);
        Assert.Equal("Helper", Assert.Single(result.OutgoingCalls).Target?.Name);
    }

    [Fact]
    public async Task OutgoingCalls_KeepUpperCapOfTwoHundred()
    {
        var statements = string.Join("\n", Enumerable.Range(0, 210).Select(static _ => "Helper(1);"));
        using var fixture = Fixture.Create("public int Analyze(int input) { " + statements + " return input; }");
        var result = await fixture.AnalyzeAsync("Caller.cs", "Analyze(4)", 500);
        Assert.Null(result.FailureReason);
        Assert.Equal(200, result.OutgoingCalls.Count);
        Assert.Equal(200, result.OutgoingCalls.Select(static edge => edge.Location).Distinct().Count());
    }

    [Theory]
    [InlineData("public int Analyze(int input) => input;", "The resolved callable symbol does not contain a block body that supports flow analysis.")]
    [InlineData("public void Analyze(int input) { }", "The resolved callable symbol does not contain analyzable statements.")]
    public async Task UnsupportedBody_PreservesSignatureAndClearsPartialAnalysis(string method, string expectedFailure)
    {
        using var fixture = Fixture.Create(method);
        var result = await fixture.AnalyzeAsync("Decl.cs", "Analyze(int input)");
        Assert.Equal(expectedFailure, result.FailureReason);
        Assert.Equal("Analyze", result.Signature?.Name);
        Assert.NotNull(result.Guidance);
        Assert.Null(result.DataFlow);
        Assert.Null(result.ControlFlow);
        Assert.Empty(result.OutgoingCalls);
    }

    [Fact]
    public async Task MetadataCallable_PreservesSignatureWhenSourceIsUnavailable()
    {
        using var fixture = Fixture.Create();
        var result = await fixture.AnalyzeAsync("Caller.cs", "ToString()");
        Assert.Equal("The resolved callable symbol does not have source syntax available.", result.FailureReason);
        Assert.Equal("ToString", result.Signature?.Name);
        Assert.Null(result.DataFlow);
        Assert.Null(result.ControlFlow);
        Assert.Empty(result.OutgoingCalls);
    }

    private sealed class Fixture(AdhocWorkspace workspace, Solution solution, ServiceProvider provider,
        string root, IReadOnlyDictionary<string, string> sources) : IDisposable
    {
        private const string DefaultMethod = "public int Analyze(int input) { var value = Helper(input); var thing = new Thing(); return value + thing.Value; }";
        public Solution Solution => solution;
        public string PathFor(string file) => Path.Combine(root, file);

        public static Fixture Create(string method = DefaultMethod)
        {
            var workspace = new AdhocWorkspace();
            try
            {
                var root = Path.Combine(Path.GetTempPath(), "method-analysis-query-tests", Guid.NewGuid().ToString("N"));
                var sources = new Dictionary<string, string>
                {
                    ["Decl.cs"] = "namespace Sample; public class Worker { " + method + " private static int Helper(int value) { return value; } } public class Thing { public int Value => 2; }",
                    ["Caller.cs"] = "namespace Sample; public class Caller { public void Call(Worker worker, object value) { worker.Analyze(4); value.ToString(); } }"
                };
                var project = workspace.AddProject("Sample", LanguageNames.CSharp);
                var solution = project.Solution
                    .WithProjectCompilationOptions(project.Id, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .AddMetadataReference(project.Id, MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
                foreach (var (file, source) in sources)
                    solution = solution.AddDocument(DocumentId.CreateNewId(project.Id), file, SourceText.From(source), filePath: Path.Combine(root, file));
                var sessions = new Sessions(new RoslynWorkspaceSession(null!, workspace, solution));
                var provider = new ServiceCollection().AddLogging().AddRoslynMcpServer()
                    .AddSingleton<IWorkspaceSessionProvider>(sessions).BuildServiceProvider();
                return new Fixture(workspace, solution, provider, root, sources);
            }
            catch
            {
                workspace.Dispose();
                throw;
            }
        }

        public ValueTask<AnalyzeMethodResult> AnalyzeAsync(string file, string marker, int maxCalls = 100)
        {
            var text = SourceText.From(sources[file]);
            var index = sources[file].IndexOf(marker, StringComparison.Ordinal);
            Assert.True(index >= 0);
            var position = text.Lines.GetLinePosition(index);
            return provider.GetRequiredService<IMethodAnalysisQueryService>().AnalyzeMethodAsync(
                new AnalyzeMethodRequest(Path.Combine(root, "Sample.csproj"), PathFor(file), position.Line + 1, position.Character + 1, maxCalls));
        }

        public async Task<string[]> TextsAsync()
        {
            var texts = new List<string>();
            foreach (var document in solution.Projects.SelectMany(static project => project.Documents))
                texts.Add((await document.GetTextAsync()).ToString());
            return texts.ToArray();
        }

        public void Dispose()
        {
            provider.Dispose();
            workspace.Dispose();
        }
    }

    private sealed class Sessions(RoslynWorkspaceSession session) : IWorkspaceSessionProvider
    {
        public ValueTask<RoslynWorkspaceSession?> GetReadySessionAsync(string solutionPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<RoslynWorkspaceSession?>(session);
        }

        public ValueTask<LoadedWorkspaceSnapshot?> GetReadySnapshotAsync(string solutionPath, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<LoadedWorkspaceSnapshot?>(null);
    }
}
