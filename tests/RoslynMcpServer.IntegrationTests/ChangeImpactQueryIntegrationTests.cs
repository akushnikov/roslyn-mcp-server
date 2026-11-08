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

public sealed class ChangeImpactQueryIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequestedCancellation_AfterDependentTraversalStarts_IsPropagatedOrProjected(bool throughService)
    {
        using var fixture = ImpactWorkspace.Create(blockTraversal: true);
        using var cancellation = new CancellationTokenSource();
        var task = throughService
            ? fixture.Provider.GetRequiredService<IChangeImpactQueryService>()
                .AnalyzeChangeImpactAsync(fixture.Request, cancellation.Token).AsTask()
            : fixture.Provider.GetRequiredService<IChangeImpactQueryDataProvider>()
                .AnalyzeChangeImpactAsync(fixture.Request, cancellation.Token).AsTask();

        try
        {
            await fixture.Loader!.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(task.IsCompleted);
            cancellation.Cancel();
            if (throughService)
            {
                var result = await task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Null(result.Symbol);
                Assert.Null(result.Impact);
                Assert.Equal("The operation was canceled.", result.FailureReason);
                Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
            }
            else
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => task.WaitAsync(TimeSpan.FromSeconds(10)));
            }
        }
        finally
        {
            cancellation.Cancel();
        }
    }

    [Fact]
    public async Task SuccessfulImpact_PreservesSourceContentsCountsAndSortedDistinctFiles()
    {
        using var fixture = ImpactWorkspace.Create();
        var service = fixture.Provider.GetRequiredService<IChangeImpactQueryService>();
        var result = await service.AnalyzeChangeImpactAsync(fixture.Request);

        Assert.Null(result.FailureReason);
        Assert.Equal("Execute", result.Symbol?.Name);
        Assert.NotNull(result.Impact);
        var impact = result.Impact;
        Assert.NotEmpty(impact.References);
        Assert.NotEmpty(impact.Callers);
        Assert.NotEmpty(impact.Implementations);
        Assert.Equal(impact.References.Count, impact.ReferenceCount);
        Assert.Equal(impact.Callers.Count, impact.CallerCount);
        Assert.Equal(impact.Implementations.Count, impact.ImplementationCount);
        Assert.All(impact.Callers, caller =>
        {
            Assert.Contains("Call", caller.CallingSymbol);
            Assert.Contains("worker.Execute()", caller.LineText);
            Assert.Equal(fixture.DependentPath, caller.Location.FilePath);
        });
        Assert.All(impact.References, reference => Assert.True(reference.Location.Line > 0));
        Assert.All(impact.Implementations, implementation => Assert.NotNull(implementation.Definition));
        var expectedFiles = impact.References.Select(static reference => reference.Location.FilePath)
            .Concat(impact.Callers.Select(static caller => caller.Location.FilePath))
            .Concat(impact.Implementations.Select(static implementation => implementation.Definition!.FilePath))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expectedFiles, impact.ImpactedFiles);

        var limited = await service.AnalyzeChangeImpactAsync(fixture.Request with { MaxResults = 1 });
        Assert.Null(limited.FailureReason);
        Assert.NotNull(limited.Impact);
        Assert.Single(limited.Impact.References);
        Assert.Single(limited.Impact.Callers);
        Assert.Single(limited.Impact.Implementations);
        Assert.Equal(impact.References.Take(1), limited.Impact.References);
        Assert.Equal(impact.Callers.Take(1), limited.Impact.Callers);
        Assert.Equal(impact.Implementations.Take(1), limited.Impact.Implementations);
    }

    [Fact]
    public async Task LargeImpact_KeepsEachResultCategoryCappedAtTwoHundred()
    {
        using var fixture = ImpactWorkspace.Create(implementationCount: 205);

        var result = await fixture.Provider.GetRequiredService<IChangeImpactQueryService>()
            .AnalyzeChangeImpactAsync(fixture.Request with { MaxResults = 1000 });

        Assert.Null(result.FailureReason);
        Assert.NotNull(result.Impact);
        Assert.Equal(200, result.Impact.ReferenceCount);
        Assert.Equal(200, result.Impact.CallerCount);
        Assert.Equal(200, result.Impact.ImplementationCount);
        Assert.Equal(200, result.Impact.Implementations.Select(static symbol => symbol.DisplayName).Distinct().Count());
    }

    [Fact]
    public async Task UnavailableSource_ReturnsNoSymbolOrPartialImpact()
    {
        using var fixture = ImpactWorkspace.Create();

        var result = await fixture.Provider.GetRequiredService<IChangeImpactQueryService>()
            .AnalyzeChangeImpactAsync(fixture.Request with { FilePath = fixture.DeclarationPath + ".missing" });

        Assert.Null(result.Symbol);
        Assert.Null(result.Impact);
        Assert.Equal("The requested file was not found in the loaded solution.", result.FailureReason);
        Assert.NotNull(result.Guidance);
    }

    [Fact]
    public async Task UnexpectedSessionFault_ReturnsSafeOrdinaryFailure()
    {
        using var fixture = ImpactWorkspace.Create(failSession: true);

        var result = await fixture.Provider.GetRequiredService<IChangeImpactQueryService>()
            .AnalyzeChangeImpactAsync(fixture.Request);

        Assert.Null(result.Symbol);
        Assert.Null(result.Impact);
        Assert.Equal("The server could not compute a semantic impact summary for the resolved symbol.", result.FailureReason);
        Assert.Equal("Retry the request on a source symbol inside the loaded solution.", result.Guidance);
        Assert.DoesNotContain("sensitive", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Queries an immutable solution so initialization never eagerly loads the blocked dependent file.</summary>
    private sealed class ImpactWorkspace(AdhocWorkspace workspace, ServiceProvider provider,
        string projectPath, string declarationPath, string dependentPath, TraversalTextLoader? loader) : IDisposable
    {
        private const string Declaration = "namespace Sample; public interface IWorker { int Execute(); }";
        public ServiceProvider Provider => provider;
        public string DeclarationPath => declarationPath;
        public string DependentPath => dependentPath;
        public TraversalTextLoader? Loader => loader;
        public AnalyzeChangeImpactRequest Request => new(projectPath, declarationPath, 1,
            Declaration.IndexOf("Execute", StringComparison.Ordinal) + 1);

        public static ImpactWorkspace Create(bool blockTraversal = false, int implementationCount = 2, bool failSession = false)
        {
            var workspace = new AdhocWorkspace();
            try
            {
                var root = Path.Combine(Path.GetTempPath(), "roslyn-mcp-impact-tests", Guid.NewGuid().ToString("N"));
                var projectPath = Path.Combine(root, "Contract.csproj");
                var declarationPath = Path.Combine(root, "Contract.cs");
                var dependentPath = Path.Combine(root, "Dependent.cs");
                var contract = workspace.AddProject("Contract", LanguageNames.CSharp);
                var reference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
                var solution = contract.Solution
                    .WithProjectCompilationOptions(contract.Id, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .AddMetadataReference(contract.Id, reference)
                    .AddDocument(DocumentId.CreateNewId(contract.Id), "Contract.cs", SourceText.From(Declaration), filePath: declarationPath);
                var dependentId = ProjectId.CreateNewId();
                solution = solution.AddProject(dependentId, "Dependent", "Dependent", LanguageNames.CSharp)
                    .WithProjectCompilationOptions(dependentId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .AddMetadataReference(dependentId, reference)
                    .AddProjectReference(dependentId, new ProjectReference(contract.Id));
                TraversalTextLoader? loader = null;
                var dependentDocumentId = DocumentId.CreateNewId(dependentId);
                if (blockTraversal)
                {
                    loader = new TraversalTextLoader();
                    solution = solution.AddDocument(DocumentInfo.Create(dependentDocumentId, "Dependent.cs",
                        loader: loader, filePath: dependentPath));
                }
                else
                {
                    var implementations = Enumerable.Range(0, implementationCount).Select(index =>
                        $"public class Worker{index} : IWorker {{ public int Execute() => {index}; public int Call(IWorker worker) => worker.Execute(); }}");
                    solution = solution.AddDocument(dependentDocumentId, "Dependent.cs",
                        SourceText.From("namespace Sample;\n" + string.Join("\n", implementations)), filePath: dependentPath);
                }

                var session = new RoslynWorkspaceSession(null!, workspace, solution);
                var provider = new ServiceCollection().AddLogging().AddRoslynMcpServer()
                    .AddSingleton<IWorkspaceSessionProvider>(new ControlledSessionProvider(session, failSession))
                    .BuildServiceProvider();
                return new ImpactWorkspace(workspace, provider, projectPath, declarationPath, dependentPath, loader);
            }
            catch
            {
                workspace.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            provider.Dispose();
            workspace.Dispose();
        }
    }

    private sealed class TraversalTextLoader : TextLoader
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<TextAndVersion> LoadTextAndVersionAsync(LoadTextOptions options, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Traversal must be released by cancellation.");
        }
    }

    private sealed class ControlledSessionProvider(RoslynWorkspaceSession session, bool failSession) : IWorkspaceSessionProvider
    {
        public ValueTask<RoslynWorkspaceSession?> GetReadySessionAsync(string solutionPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return failSession
                ? throw new InvalidOperationException("sensitive session details")
                : ValueTask.FromResult<RoslynWorkspaceSession?>(session);
        }

        public ValueTask<LoadedWorkspaceSnapshot?> GetReadySnapshotAsync(string solutionPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<LoadedWorkspaceSnapshot?>(null);
        }
    }
}
