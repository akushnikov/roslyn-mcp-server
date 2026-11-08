using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Infrastructure.Models;
using RoslynMcpServer.Abstractions.Infrastructure.Services;
using RoslynMcpServer.Abstractions.Server.Results;
using RoslynMcpServer.Abstractions.Server.Services;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Application.Infrastructure;
using RoslynMcpServer.Application.Infrastructure.Operations;

namespace RoslynMcpServer.UnitTests.Infrastructure;

public sealed class DiagnoseQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_NullRequest_RejectsBeforeDependencies()
    {
        var fixture = new DiagnoseQueryFixture();
        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Operation.ExecuteAsync(null!).AsTask());
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public async Task ExecuteAsync_BlankOptionalPath_SkipsProbe(string? path)
    {
        var fixture = new DiagnoseQueryFixture();
        var (success, error, canceled) = await fixture.Operation.ExecuteAsync(new(path, true));
        Assert.NotNull(success);
        Assert.Same(fixture.Server, success.Value.Value.Server);
        Assert.Same(fixture.Environment, success.Value.Value.Environment);
        Assert.Null(success.Value.Value.Workspace);
        Assert.Empty(success.Value.Value.Diagnostics);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Equal(new[] { "server", "environment" }, fixture.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_PreservesOrderPathAndNestedDiagnostics(bool verbose)
    {
        var fixture = new DiagnoseQueryFixture();
        using var cts = new CancellationTokenSource();
        var (success, error, canceled) = await fixture.Operation.ExecuteAsync(new("  original.sln  ", verbose), cts.Token);
        Assert.NotNull(success);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Same(fixture.Workspace, success.Value.Value.Workspace);
        Assert.Equal("  original.sln  ", fixture.ReceivedPath);
        Assert.Equal(new[] { "server", "environment", "workspace" }, fixture.Calls);
        Assert.All(fixture.Tokens, token => Assert.Equal(cts.Token, token));
        Assert.Equal(2, fixture.Workspace.Diagnostics.Count);
        if (verbose)
            Assert.Same(fixture.Workspace.Diagnostics, success.Value.Value.Diagnostics);
        else
            Assert.Equal(new[] { fixture.Workspace.Diagnostics[0] }, success.Value.Value.Diagnostics);
    }

    [Theory]
    [InlineData("PathNotFound")]
    [InlineData("UnsupportedWorkspacePath")]
    [InlineData("WorkspaceLoadFailed")]
    public async Task ExecuteAsync_UnhealthyWorkspace_RemainsSuccessfulData(string code)
    {
        var fixture = new DiagnoseQueryFixture
        {
            Workspace = new("missing.sln", false, 0, 0,
                [new(WorkspaceDiagnosticSeverity.Error, code, "Original diagnostic", "missing.sln")])
        };
        var (success, error, canceled) = await fixture.Operation.ExecuteAsync(new("missing.sln", false));
        Assert.NotNull(success);
        Assert.Same(fixture.Workspace, success.Value.Value.Workspace);
        Assert.False(success.Value.Value.Workspace!.CanBeLoaded);
        Assert.Equal(code, Assert.Single(success.Value.Value.Diagnostics).Code);
        Assert.Null(error);
        Assert.Null(canceled);
    }

    [Theory]
    [InlineData("server")]
    [InlineData("environment")]
    [InlineData("workspace")]
    public async Task ExecuteAsync_DependencyFault_RetainsOriginalException(string stage)
    {
        var original = new InvalidOperationException("Original fault");
        var fixture = new DiagnoseQueryFixture { FaultStage = stage, Fault = original };
        var (success, error, canceled) = await fixture.Operation.ExecuteAsync(new("sample.sln", false));
        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Same(original, error.Value.Value.SourceException);
        Assert.Null(canceled);
        Assert.Equal(stage, fixture.Calls.Last());
    }

    [Fact]
    public async Task ExecuteAsync_PreCanceled_SkipsAllDependencies()
    {
        var fixture = new DiagnoseQueryFixture();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (success, error, canceled) = await fixture.Operation.ExecuteAsync(new("sample.sln", false), cts.Token);
        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData("server", false)]
    [InlineData("environment", false)]
    [InlineData("workspace", false)]
    [InlineData("server", true)]
    [InlineData("environment", true)]
    [InlineData("workspace", true)]
    public async Task ExecuteAsync_InFlightCancellation_IsCanceledEvenIfDependencyReturns(string stage, bool fallback)
    {
        var fixture = new DiagnoseQueryFixture { PauseStage = stage, ReturnAfterCancellation = fallback };
        using var cts = new CancellationTokenSource();
        var pending = fixture.Operation.ExecuteAsync(new("sample.sln", false), cts.Token).AsTask();
        await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cts.Cancel();
        fixture.Release.SetResult();
        var (success, error, canceled) = await pending;
        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(stage, fixture.Calls.Last());
    }

    [Fact]
    public async Task ExecuteAsync_UnrelatedCancellation_Propagates()
    {
        using var other = new CancellationTokenSource();
        other.Cancel();
        var original = new OperationCanceledException(other.Token);
        var fixture = new DiagnoseQueryFixture { FaultStage = "environment", Fault = original };
        var actual = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Operation.ExecuteAsync(new(null, false)).AsTask());
        Assert.Same(original, actual);
    }
}

internal sealed class DiagnoseQueryFixture
{
    public GetServerInfoResult Server { get; } = new("server", "1", "framework", "os", DateTimeOffset.UnixEpoch);
    public EnvironmentDescriptor Environment { get; } = new("9", true, "Registered");
    public WorkspaceHealthDescriptor Workspace { get; init; } = new("sample.sln", true, 1, 2,
        [new(WorkspaceDiagnosticSeverity.Error, "Error", "error", "sample.sln"),
         new(WorkspaceDiagnosticSeverity.Warning, "Warning", "warning", "sample.sln")]);
    public List<string> Calls { get; } = [];
    public List<CancellationToken> Tokens { get; } = [];
    public string? ReceivedPath { get; private set; }
    public string? FaultStage { get; init; }
    public Exception? Fault { get; init; }
    public string? PauseStage { get; init; }
    public bool ReturnAfterCancellation { get; init; }
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public DiagnoseQueryOperation Operation => new(new Metadata(this), new Runtime(this), NullLogger<DiagnoseQueryOperation>.Instance);
    public DiagnoseQueryService Service => new(Operation);

    private async ValueTask BeforeAsync(string stage, CancellationToken token)
    {
        Calls.Add(stage);
        Tokens.Add(token);
        if (stage == PauseStage)
        {
            Entered.SetResult();
            await Release.Task;
            if (!ReturnAfterCancellation)
                token.ThrowIfCancellationRequested();
        }
        if (stage == FaultStage)
            ThrowDependencyFault(Fault!);
    }

    internal static void ThrowDependencyFault(Exception fault) => throw fault;

    private sealed class Metadata(DiagnoseQueryFixture fixture) : IServerInfoQueryDataProvider
    {
        public async ValueTask<GetServerInfoResult> GetServerInfoAsync(CancellationToken cancellationToken = default)
        {
            await fixture.BeforeAsync("server", cancellationToken);
            return fixture.Server;
        }
    }

    private sealed class Runtime(DiagnoseQueryFixture fixture) : IRuntimeEnvironmentService
    {
        public async ValueTask<EnvironmentDescriptor> GetEnvironmentAsync(CancellationToken cancellationToken = default)
        {
            await fixture.BeforeAsync("environment", cancellationToken);
            return fixture.Environment;
        }

        public async ValueTask<WorkspaceHealthDescriptor> ProbeWorkspaceAsync(string solutionPath, CancellationToken cancellationToken = default)
        {
            fixture.ReceivedPath = solutionPath;
            await fixture.BeforeAsync("workspace", cancellationToken);
            return fixture.Workspace;
        }
    }
}
