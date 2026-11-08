using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Infrastructure.DependencyInjection;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;

namespace RoslynMcpServer.IntegrationTests;

public sealed class AnalysisQueryIntegrationTests
{
    [Fact]
    public void ProductionRegistration_ResolvesDedicatedQueriesAndRetainedLegacyService()
    {
        using var provider = CreateProvider();

        Assert.NotNull(provider.GetRequiredService<IDiagnosticsQueryService>());
        Assert.NotNull(provider.GetRequiredService<IFindCallersQueryService>());
        Assert.NotNull(provider.GetRequiredService<IFindImplementationsQueryService>());
        Assert.NotNull(provider.GetRequiredService<ITypeHierarchyQueryService>());
        Assert.NotNull(provider.GetRequiredService<IAnalysisService>());
        Assert.Equal(new[]
        {
            "AnalyzeChangeImpactAsync", "AnalyzeMethodAsync", "CheckTypeCompatibilityAsync",
            "GetOutgoingCallsAsync", "ValidateCodeAsync"
        }, typeof(IAnalysisService).GetMethods().Select(static method => method.Name).Order());
    }

    [Theory]
    [InlineData("Diagnostics", nameof(IDiagnosticsQueryService))]
    [InlineData("FindCallers", nameof(IFindCallersQueryService))]
    [InlineData("FindImplementations", nameof(IFindImplementationsQueryService))]
    [InlineData("TypeHierarchy", nameof(ITypeHierarchyQueryService))]
    public void AdapterConstructor_BindsDedicatedService(string toolName, string serviceType)
    {
        var path = Path.Combine(FindRepoRoot(), "src", "RoslynMcpServer.Mcp", "Analysis", "Tools", toolName + "Tool.cs");
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();
        var adapter = Assert.Single(root.DescendantNodes().OfType<ClassDeclarationSyntax>());

        Assert.NotNull(adapter.ParameterList);
        Assert.Equal(serviceType, Assert.Single(adapter.ParameterList.Parameters).Type?.ToString());
    }

    [Fact]
    public async Task Diagnostics_PreserveSolutionFileSeverityAndPositionOrdering()
    {
        await using var fixture = await TestWorkspace.CreateAsync();
        var service = fixture.Provider.GetRequiredService<IDiagnosticsQueryService>();
        var all = await service.GetDiagnosticsAsync(new(fixture.ProjectPath, FilePath: null, SeverityFilter: "All"));

        Assert.Null(all.FailureReason);
        Assert.Contains(all.Diagnostics, diagnostic => diagnostic.Id == "CS0103" && diagnostic.Location?.FilePath == fixture.PathFor("AErrors.cs"));
        Assert.Contains(all.Diagnostics, diagnostic => diagnostic.Id == "CS0103" && diagnostic.Location?.FilePath == fixture.PathFor("ZErrors.cs"));
        Assert.Contains(all.Diagnostics, static diagnostic => diagnostic.Id == "CS1030" && diagnostic.Severity == "Warning");
        Assert.Equal(all.Diagnostics.OrderBy(static diagnostic => diagnostic.Location?.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static diagnostic => diagnostic.Location?.Line ?? int.MaxValue)
            .ThenBy(static diagnostic => diagnostic.Location?.Column ?? int.MaxValue), all.Diagnostics);

        var file = await service.GetDiagnosticsAsync(new(fixture.ProjectPath, fixture.PathFor("AErrors.cs"), "eRrOr"));
        Assert.Null(file.FailureReason);
        Assert.Equal(2, file.Diagnostics.Count);
        Assert.All(file.Diagnostics, diagnostic =>
        {
            Assert.Equal("Error", diagnostic.Severity);
            Assert.Equal("CS0103", diagnostic.Id);
            Assert.Equal(fixture.PathFor("AErrors.cs"), diagnostic.Location?.FilePath);
        });
        Assert.True(file.Diagnostics[0].Location!.Column < file.Diagnostics[1].Location!.Column);

        var warnings = await service.GetDiagnosticsAsync(new(fixture.ProjectPath, FilePath: null, SeverityFilter: "wArNiNg"));
        Assert.NotEmpty(warnings.Diagnostics);
        Assert.All(warnings.Diagnostics, static diagnostic => Assert.Equal("Warning", diagnostic.Severity));
        var whitespace = await service.GetDiagnosticsAsync(new(fixture.ProjectPath, FilePath: null, SeverityFilter: " "));
        Assert.Equal(all.Diagnostics, whitespace.Diagnostics);
        foreach (var filter in new[] { " Error ", "unknown" })
        {
            var filtered = await service.GetDiagnosticsAsync(new(fixture.ProjectPath, FilePath: null, SeverityFilter: filter));
            Assert.Null(filtered.FailureReason);
            Assert.Empty(filtered.Diagnostics);
        }
    }

    [Fact]
    public async Task Hierarchy_PreservesContainingTypeAncestorsDescendantsAndDirection()
    {
        await using var fixture = await TestWorkspace.CreateAsync();
        var service = fixture.Provider.GetRequiredService<ITypeHierarchyQueryService>();
        var position = fixture.Position("Types.cs", "Execute() => 2");
        var request = new GetTypeHierarchyRequest(fixture.ProjectPath, fixture.PathFor("Types.cs"), position.Line, position.Column, Direction: null);

        var both = await service.GetTypeHierarchyAsync(request);
        Assert.Null(both.FailureReason);
        Assert.Equal("Derived", both.Symbol?.Name);
        Assert.Equal("Worker", both.BaseTypes[0].Name);
        Assert.Contains(both.Interfaces, static symbol => symbol.Name == "IWorker");
        Assert.Contains(both.DerivedTypes, static symbol => symbol.Name == "Leaf");

        var ancestors = await service.GetTypeHierarchyAsync(request with { Direction = " aNcEsToRs " });
        Assert.Equal(both.BaseTypes, ancestors.BaseTypes);
        Assert.Equal(both.Interfaces, ancestors.Interfaces);
        Assert.Empty(ancestors.DerivedTypes);
        var descendants = await service.GetTypeHierarchyAsync(request with { Direction = " Descendants " });
        Assert.Equal(both.DerivedTypes, descendants.DerivedTypes);
        Assert.Empty(descendants.BaseTypes);
        Assert.Empty(descendants.Interfaces);
        var unknown = await service.GetTypeHierarchyAsync(request with { Direction = "unknown" });
        Assert.Null(unknown.FailureReason);
        Assert.Equal(both.Symbol, unknown.Symbol);
        Assert.Empty(unknown.BaseTypes);
        Assert.Empty(unknown.DerivedTypes);
        Assert.Empty(unknown.Interfaces);

        position = fixture.Position("Types.cs", "IWorker {");
        var contract = await service.GetTypeHierarchyAsync(request with
        {
            Line = position.Line, Column = position.Column, Direction = "Descendants"
        });
        Assert.Equal("IWorker", contract.Symbol?.Name);
        Assert.Contains(contract.DerivedTypes, static symbol => symbol.Name == "Worker");
        Assert.Contains(contract.DerivedTypes, static symbol => symbol.Name == "Derived");
    }

    [Fact]
    public async Task ImplementationsAndCallers_ReturnKnownSourceDefinitionsAndRespectLimits()
    {
        await using var fixture = await TestWorkspace.CreateAsync();
        var implementationsService = fixture.Provider.GetRequiredService<IFindImplementationsQueryService>();
        var callersService = fixture.Provider.GetRequiredService<IFindCallersQueryService>();
        var position = fixture.Position("Types.cs", "Execute();");
        var implementationsRequest = new FindImplementationsRequest(fixture.ProjectPath, fixture.PathFor("Types.cs"), position.Line, position.Column);
        var implementations = await implementationsService.FindImplementationsAsync(implementationsRequest);

        Assert.Null(implementations.FailureReason);
        Assert.Equal("Execute", implementations.Symbol?.Name);
        Assert.Contains(implementations.Implementations, symbol => symbol.ContainerName == "Sample.Worker" && symbol.Definition?.FilePath == fixture.PathFor("Types.cs"));
        Assert.All(implementations.Implementations, symbol => Assert.NotNull(symbol.Definition));
        var oneImplementation = await implementationsService.FindImplementationsAsync(implementationsRequest with { MaxResults = 1 });
        Assert.Single(oneImplementation.Implementations);

        position = fixture.Position("Types.cs", "Execute() => 1");
        var callersRequest = new FindCallersRequest(fixture.ProjectPath, fixture.PathFor("Types.cs"), position.Line, position.Column);
        var callers = await callersService.FindCallersAsync(callersRequest);
        Assert.Null(callers.FailureReason);
        Assert.Equal("Execute", callers.Symbol?.Name);
        Assert.True(callers.Callers.Count >= 2);
        Assert.Contains(callers.Callers, caller => caller.CallingSymbol?.Contains("First", StringComparison.Ordinal) == true);
        Assert.All(callers.Callers, caller =>
        {
            Assert.Equal(fixture.PathFor("Calls.cs"), caller.Location.FilePath);
            Assert.Contains("worker.Execute()", caller.LineText);
        });
        var oneCaller = await callersService.FindCallersAsync(callersRequest with { MaxResults = 1 });
        Assert.Single(oneCaller.Callers);
    }

    [Fact]
    public async Task LargeSourceSets_KeepBothProviderCapsAtTwoHundred()
    {
        await using var fixture = await TestWorkspace.CreateAsync(includeLargeSourceSet: true);
        var position = fixture.Position("Types.cs", "IWorker {");
        var implementations = await fixture.Provider.GetRequiredService<IFindImplementationsQueryService>()
            .FindImplementationsAsync(new(fixture.ProjectPath, fixture.PathFor("Types.cs"), position.Line, position.Column, MaxResults: 1000));
        Assert.Null(implementations.FailureReason);
        Assert.Equal(200, implementations.Implementations.Count);
        Assert.Equal(200, implementations.Implementations.Select(static symbol => symbol.DisplayName).Distinct().Count());

        position = fixture.Position("Types.cs", "Execute() => 1");
        var callers = await fixture.Provider.GetRequiredService<IFindCallersQueryService>()
            .FindCallersAsync(new(fixture.ProjectPath, fixture.PathFor("Types.cs"), position.Line, position.Column, MaxResults: 1000));
        Assert.Null(callers.FailureReason);
        Assert.Equal(200, callers.Callers.Count);
        Assert.Equal(200, callers.Callers.Select(static caller => caller.CallingSymbol).Distinct().Count());
    }

    [Fact]
    public async Task RetainedValidateCode_UsesDedicatedAllDiagnosticsWithoutChangingCountsOrFailures()
    {
        await using var fixture = await TestWorkspace.CreateAsync();
        var diagnostics = fixture.Provider.GetRequiredService<IDiagnosticsQueryService>();
        var legacy = fixture.Provider.GetRequiredService<IAnalysisService>();
        foreach (var filePath in new[] { null, fixture.PathFor("AErrors.cs"), fixture.PathFor("Types.cs") })
        {
            var expected = await diagnostics.GetDiagnosticsAsync(new(fixture.ProjectPath, filePath, "All"));
            var actual = await legacy.ValidateCodeAsync(new(fixture.ProjectPath, filePath));
            Assert.Null(actual.FailureReason);
            Assert.Equal(expected.Diagnostics, actual.Diagnostics);
            Assert.Equal(expected.Diagnostics.Count(static diagnostic => diagnostic.Severity == "Error"), actual.ErrorCount);
            Assert.Equal(expected.Diagnostics.Count(static diagnostic => diagnostic.Severity == "Warning"), actual.WarningCount);
            Assert.Equal(actual.ErrorCount == 0, actual.IsValid);
            Assert.Equal(actual.IsValid ? null : "Resolve compiler errors before applying additional semantic edits.", actual.Guidance);
        }

        var missingPath = fixture.PathFor("Absent.cs");
        var failed = await diagnostics.GetDiagnosticsAsync(new(fixture.ProjectPath, missingPath, "All"));
        var invalid = await legacy.ValidateCodeAsync(new(fixture.ProjectPath, missingPath));
        Assert.NotNull(failed.FailureReason);
        Assert.False(invalid.IsValid);
        Assert.Equal(0, invalid.ErrorCount);
        Assert.Equal(0, invalid.WarningCount);
        Assert.Empty(invalid.Diagnostics);
        Assert.Equal(failed.FailureReason, invalid.FailureReason);
        Assert.Equal(failed.Guidance, invalid.Guidance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealCallersProvider_PropagatesCancellationAfterDependentTraversalStarts(bool throughService)
    {
        using var fixture = TraversalWorkspace.Create();
        using var cancellation = new CancellationTokenSource();
        var request = new FindCallersRequest(fixture.ProjectPath, fixture.DeclarationPath, 1, TraversalWorkspace.MethodColumn);
        Assert.False(cancellation.IsCancellationRequested);
        var task = throughService
            ? fixture.Provider.GetRequiredService<IFindCallersQueryService>().FindCallersAsync(request, cancellation.Token).AsTask()
            : fixture.Provider.GetRequiredService<IFindCallersQueryDataProvider>().FindCallersAsync(request, cancellation.Token).AsTask();
        try
        {
            await fixture.Loader.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(task.IsCompleted);
            cancellation.Cancel();
            if (throughService)
            {
                var result = await task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal("The operation was canceled.", result.FailureReason);
                Assert.Empty(result.Callers);
                Assert.Null(result.Symbol);
            }
            else
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(10)));
            }
        }
        finally
        {
            cancellation.Cancel();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealImplementationsProvider_PropagatesCancellationAfterDependentTraversalStarts(bool throughService)
    {
        using var fixture = TraversalWorkspace.Create();
        using var cancellation = new CancellationTokenSource();
        var request = new FindImplementationsRequest(fixture.ProjectPath, fixture.DeclarationPath, 1, TraversalWorkspace.MethodColumn);
        Assert.False(cancellation.IsCancellationRequested);
        var task = throughService
            ? fixture.Provider.GetRequiredService<IFindImplementationsQueryService>().FindImplementationsAsync(request, cancellation.Token).AsTask()
            : fixture.Provider.GetRequiredService<IFindImplementationsQueryDataProvider>().FindImplementationsAsync(request, cancellation.Token).AsTask();
        try
        {
            await fixture.Loader.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(task.IsCompleted);
            cancellation.Cancel();
            if (throughService)
            {
                var result = await task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal("The operation was canceled.", result.FailureReason);
                Assert.Empty(result.Implementations);
                Assert.Null(result.Symbol);
            }
            else
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(10)));
            }
        }
        finally
        {
            cancellation.Cancel();
        }
    }

    private static ServiceProvider CreateProvider() =>
        new ServiceCollection().AddLogging().AddRoslynMcpServer().BuildServiceProvider();

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "roslyn-mcp-server.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private sealed class TestWorkspace(string rootDirectory, string projectPath, ServiceProvider provider) : IAsyncDisposable
    {
        private const string TypesSource = """
            namespace Sample;
            public interface IWorker { int Execute(); }
            public class Worker : IWorker { public virtual int Execute() => 1; }
            public class Derived : Worker { public override int Execute() => 2; }
            public sealed class Leaf : Derived { }
            """;

        private const string CallsSource = """
            namespace Sample;
            public sealed class Consumer
            {
                public int First(Worker worker) => worker.Execute();
                public int Second(Worker worker) => worker.Execute();
            }
            """;

        public string ProjectPath => projectPath;
        public ServiceProvider Provider => provider;
        public string PathFor(string fileName) => Path.Combine(rootDirectory, fileName);

        public (int Line, int Column) Position(string fileName, string marker)
        {
            var text = fileName == "Types.cs" ? TypesSource : CallsSource;
            var offset = text.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(offset >= 0);
            var position = SourceText.From(text).Lines.GetLinePosition(offset);
            return (position.Line + 1, position.Character + 1);
        }

        public static async Task<TestWorkspace> CreateAsync(bool includeLargeSourceSet = false)
        {
            var root = Path.Combine(Path.GetTempPath(), "roslyn-mcp-analysis-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            ServiceProvider? provider = null;
            try
            {
                var projectPath = Path.Combine(root, "Sample.csproj");
                await File.WriteAllTextAsync(projectPath, $$"""
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>
                      <ItemGroup><Reference Include="System.Private.CoreLib"><HintPath>{{System.Security.SecurityElement.Escape(typeof(object).Assembly.Location)}}</HintPath></Reference></ItemGroup>
                    </Project>
                    """);
                await File.WriteAllTextAsync(Path.Combine(root, "Types.cs"), TypesSource);
                await File.WriteAllTextAsync(Path.Combine(root, "Calls.cs"), CallsSource);
                await File.WriteAllTextAsync(Path.Combine(root, "AErrors.cs"), "namespace Sample; public class AErrors { public int Fail() => MissingOne() + MissingTwo(); }");
                await File.WriteAllTextAsync(Path.Combine(root, "ZErrors.cs"), "#warning analysis fixture warning\nnamespace Sample; public class ZErrors { public int Fail() => MissingThree(); }");
                if (includeLargeSourceSet)
                {
                    var declarations = Enumerable.Range(0, 205).Select(index =>
                        $"public sealed class Implementation{index} : IWorker {{ public int Execute() => {index}; public int Call{index}(Worker worker) => worker.Execute(); }}");
                    await File.WriteAllTextAsync(Path.Combine(root, "Large.cs"), "namespace Sample;\n" + string.Join("\n", declarations));
                }

                provider = CreateProvider();
                await provider.GetRequiredService<IWorkspaceLoader>().LoadAsync(new(projectPath, ForceReload: false));
                return new TestWorkspace(root, projectPath, provider);
            }
            catch
            {
                if (provider is not null)
                {
                    await provider.DisposeAsync();
                }

                Directory.Delete(root, recursive: true);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await provider.DisposeAsync();
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Holds the dependent project's first source load until the test cancels traversal. The declaration
    /// project does not depend on it, so symbol resolution completes without touching this loader.
    /// </summary>
    private sealed class TraversalWorkspace(AdhocWorkspace workspace, ServiceProvider provider, TraversalTextLoader loader,
        string projectPath, string declarationPath) : IDisposable
    {
        private const string Declaration = "namespace Sample; public interface IWorker { int Execute(); }";
        public static int MethodColumn => Declaration.IndexOf("Execute", StringComparison.Ordinal) + 1;
        public ServiceProvider Provider => provider;
        public TraversalTextLoader Loader => loader;
        public string ProjectPath => projectPath;
        public string DeclarationPath => declarationPath;

        public static TraversalWorkspace Create()
        {
            var workspace = new AdhocWorkspace();
            try
            {
                var root = Path.Combine(Path.GetTempPath(), "roslyn-mcp-traversal-tests", Guid.NewGuid().ToString("N"));
                var projectPath = Path.Combine(root, "Contract.csproj");
                var declarationPath = Path.Combine(root, "Contract.cs");
                var declarationProject = workspace.AddProject("Contract", LanguageNames.CSharp);
                var reference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
                var solution = declarationProject.Solution
                    .WithProjectCompilationOptions(declarationProject.Id, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .AddMetadataReference(declarationProject.Id, reference)
                    .AddDocument(DocumentId.CreateNewId(declarationProject.Id), "Contract.cs", SourceText.From(Declaration), filePath: declarationPath);
                var dependentId = ProjectId.CreateNewId();
                var loader = new TraversalTextLoader();
                solution = solution.AddProject(dependentId, "Dependent", "Dependent", LanguageNames.CSharp)
                    .WithProjectCompilationOptions(dependentId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .AddMetadataReference(dependentId, reference)
                    .AddProjectReference(dependentId, new ProjectReference(declarationProject.Id))
                    .AddDocument(DocumentInfo.Create(DocumentId.CreateNewId(dependentId), "Dependent.cs",
                        loader: loader, filePath: Path.Combine(root, "Dependent.cs")));
                // Applying this snapshot would synchronously load the deliberately blocked document.
                // Providers can query the immutable snapshot without mutating the workspace first.
                var sessionProvider = new ControlledSessionProvider(new RoslynWorkspaceSession(null!, workspace, solution));
                var provider = new ServiceCollection().AddLogging().AddRoslynMcpServer()
                    .AddSingleton<IWorkspaceSessionProvider>(sessionProvider).BuildServiceProvider();
                return new TraversalWorkspace(workspace, provider, loader, projectPath, declarationPath);
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

    private sealed class ControlledSessionProvider(RoslynWorkspaceSession session) : IWorkspaceSessionProvider
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
