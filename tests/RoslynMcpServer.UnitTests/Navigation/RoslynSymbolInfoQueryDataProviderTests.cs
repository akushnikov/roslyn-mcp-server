using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Navigation.Models;
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


    [Fact]
    public async Task GetSymbolInfoAsync_UnloadedSession_PreservesFailure()
    {
        var result = await CreateService(new TestSessionProvider(null))
            .GetSymbolInfoAsync(new GetSymbolInfoRequest(Path.GetFullPath("Missing.sln"), FilePath, 1, 1));

        Assert.Null(result.Symbol);
        Assert.Equal("The requested solution is not currently loaded.", result.FailureReason);
        Assert.Equal("Call load_solution first for the requested solutionPath.", result.Guidance);
    }

    [Fact]
    public async Task GetSymbolInfoAsync_MissingDocument_PreservesFailure()
    {
        using var workspace = new AdhocWorkspace();
        var session = CreateSession(workspace, "public class Sample { }");
        var result = await CreateService(new TestSessionProvider(session))
            .GetSymbolInfoAsync(Request(session, 1) with { FilePath = Path.GetFullPath("Missing.cs") });

        Assert.Null(result.Symbol);
        Assert.Equal("The requested file was not found in the loaded solution.", result.FailureReason);
        Assert.Equal("Call get_project_structure with includeDocuments=true to inspect valid file paths.", result.Guidance);
    }

    [Theory]
    [InlineData(2, 1, "The requested line is outside the document.")]
    [InlineData(1, 100, "The requested column is outside the target line.")]
    public async Task GetSymbolInfoAsync_OutOfRangeCoordinate_PreservesFailure(int line, int column, string failure)
    {
        using var workspace = new AdhocWorkspace();
        var session = CreateSession(workspace, "public class Sample { }");
        var result = await CreateService(new TestSessionProvider(session))
            .GetSymbolInfoAsync(Request(session, column) with { Line = line });

        Assert.Null(result.Symbol);
        Assert.Equal(failure, result.FailureReason);
        Assert.Equal("Use 1-based line and column values within the requested file.", result.Guidance);
    }

    [Fact]
    public async Task GetSymbolInfoAsync_EmptyDocument_PreservesNoSymbolFailure()
    {
        using var workspace = new AdhocWorkspace();
        var session = CreateSession(workspace, "");
        var result = await CreateService(new TestSessionProvider(session)).GetSymbolInfoAsync(Request(session, 1));

        Assert.Null(result.Symbol);
        Assert.Equal("No symbol was resolved at the requested source position.", result.FailureReason);
        Assert.Equal("Move the cursor onto a declaration or reference token and try again.", result.Guidance);
    }

    [Fact]
    public async Task GetSymbolInfoAsync_PartialType_PreservesLocationOrderAndFirstDefinition()
    {
        const string source = "public partial class Sample { }";
        using var workspace = new AdhocWorkspace();
        var firstSession = CreateSession(workspace, source);
        var secondPath = Path.GetFullPath("SymbolInfoSample.Part2.cs");
        var secondDocument = firstSession.Solution.Projects.Single()
            .AddDocument("SymbolInfoSample.Part2.cs", SourceText.From(source), filePath: secondPath);
        var session = new RoslynWorkspaceSession(firstSession.Snapshot, workspace, secondDocument.Project.Solution);
        var helper = new SymbolResolutionHelper(new TestSessionProvider(session));
        var resolved = await helper.ResolveSymbolAsync(session.Snapshot.Workspace.SolutionPath, FilePath, 1, 22, default);
        Assert.NotNull(resolved.Symbol);

        var locations = SymbolResolutionHelper.GetSourceLocations(resolved.Symbol).ToArray();
        Assert.Equal(new[] { new SymbolLocation(FilePath, 1, 22), new SymbolLocation(secondPath, 1, 22) }, locations);
        var service = CreateService(new TestSessionProvider(session));
        var first = await service.GetSymbolInfoAsync(Request(session, 22));
        var second = await service.GetSymbolInfoAsync(Request(session, 22) with { FilePath = secondPath });
        Assert.Null(first.FailureReason);
        Assert.Null(second.FailureReason);
        Assert.NotNull(first.Symbol);
        Assert.Equal(locations[0], first.Symbol.Definition);
        Assert.Equal(first.Symbol, second.Symbol);
    }

    [Fact]
    public async Task GetSymbolInfoAsync_CancellationDuringResolution_ProjectsCanceledWithoutErrorLog()
    {
        using var cancellation = new CancellationTokenSource();
        var providerLog = new TestLogger<RoslynSymbolInfoQueryDataProvider>();
        var operationLog = new TestLogger<SymbolInfoQueryOperation>();
        var sessions = new CallbackSessionProvider(token =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return null;
        });
        var provider = new RoslynSymbolInfoQueryDataProvider(new SymbolResolutionHelper(sessions), providerLog);
        var service = new SymbolInfoQueryService(new SymbolInfoQueryOperation(provider, operationLog));
        var request = new GetSymbolInfoRequest(Path.GetFullPath("Sample.sln"), FilePath, 1, 1);

        var result = await service.GetSymbolInfoAsync(request, cancellation.Token);

        Assert.Equal(1, sessions.Calls);
        Assert.Null(result.Symbol);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
        Assert.Empty(providerLog.Errors);
        Assert.Empty(operationLog.Errors);
    }

    [Fact]
    public async Task GetSymbolInfoAsync_UnexpectedSessionFault_PreservesSafePayloadAndInternalLog()
    {
        var fault = new InvalidOperationException("sensitive session details");
        var sessions = new CallbackSessionProvider(_ => throw fault);
        var providerLog = new TestLogger<RoslynSymbolInfoQueryDataProvider>();
        var provider = new RoslynSymbolInfoQueryDataProvider(new SymbolResolutionHelper(sessions), providerLog);
        var service = new SymbolInfoQueryService(new SymbolInfoQueryOperation(provider, NullLogger<SymbolInfoQueryOperation>.Instance));

        var result = await service.GetSymbolInfoAsync(new GetSymbolInfoRequest(Path.GetFullPath("Sample.sln"), FilePath, 1, 1));

        Assert.Null(result.Symbol);
        Assert.Equal("The server could not resolve symbol information.", result.FailureReason);
        Assert.Equal("Retry the request and ensure the file belongs to a loaded solution.", result.Guidance);
        var error = Assert.Single(providerLog.Errors);
        Assert.Same(fault, error.Exception);
        Assert.Contains("Failed to resolve symbol info for", error.Message);
    }

    private sealed class CallbackSessionProvider(Func<CancellationToken, RoslynWorkspaceSession?> getSession) : IWorkspaceSessionProvider
    {
        public int Calls { get; private set; }

        public ValueTask<RoslynWorkspaceSession?> GetReadySessionAsync(string solutionPath, CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult(getSession(cancellationToken));
        }

        public ValueTask<LoadedWorkspaceSnapshot?> GetReadySnapshotAsync(string solutionPath, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Symbol resolution must use the session boundary.");
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(Exception? Exception, string Message)> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                Errors.Add((exception, formatter(state, exception)));
            }
        }
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
