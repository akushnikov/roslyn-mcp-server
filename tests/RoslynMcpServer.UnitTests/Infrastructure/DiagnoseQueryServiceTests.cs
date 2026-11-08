namespace RoslynMcpServer.UnitTests.Infrastructure;

public sealed class DiagnoseQueryServiceTests
{
    [Fact]
    public async Task DiagnoseAsync_PreservesSnapshotAndNestedPayload()
    {
        var fixture = new DiagnoseQueryFixture();
        var result = await fixture.Service.DiagnoseAsync(new("sample.sln", true));
        Assert.Same(fixture.Server, result.Server);
        Assert.Same(fixture.Environment, result.Environment);
        Assert.Same(fixture.Workspace, result.Workspace);
        Assert.Same(fixture.Workspace.Diagnostics, result.Diagnostics);
    }

    [Theory]
    [InlineData("server")]
    [InlineData("environment")]
    [InlineData("workspace")]
    public async Task DiagnoseAsync_RethrowsOriginalExceptionWithStack(string stage)
    {
        var original = new InvalidOperationException("Original message");
        var fixture = new DiagnoseQueryFixture { FaultStage = stage, Fault = original };
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.DiagnoseAsync(new("sample.sln", false)).AsTask());
        Assert.Same(original, actual);
        Assert.Equal("Original message", actual.Message);
        Assert.Contains(nameof(DiagnoseQueryFixture.ThrowDependencyFault), actual.StackTrace);
    }

    [Fact]
    public async Task DiagnoseAsync_PreCancellation_RethrowsWithCallerToken()
    {
        var fixture = new DiagnoseQueryFixture();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var actual = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Service.DiagnoseAsync(new(null, false), cts.Token).AsTask());
        Assert.Equal(cts.Token, actual.CancellationToken);
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData("server")]
    [InlineData("environment")]
    [InlineData("workspace")]
    public async Task DiagnoseAsync_InFlightCancellation_RethrowsWithCallerToken(string stage)
    {
        var fixture = new DiagnoseQueryFixture { PauseStage = stage };
        using var cts = new CancellationTokenSource();
        var pending = fixture.Service.DiagnoseAsync(new("sample.sln", false), cts.Token).AsTask();
        await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cts.Cancel();
        fixture.Release.SetResult();
        var actual = await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        Assert.Equal(cts.Token, actual.CancellationToken);
        Assert.Equal(stage, fixture.Calls.Last());
    }

    [Fact]
    public async Task DiagnoseAsync_UnrelatedCancellation_RethrowsOriginal()
    {
        using var other = new CancellationTokenSource();
        other.Cancel();
        var original = new OperationCanceledException(other.Token);
        var fixture = new DiagnoseQueryFixture { FaultStage = "workspace", Fault = original };
        var actual = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Service.DiagnoseAsync(new("sample.sln", false)).AsTask());
        Assert.Same(original, actual);
        Assert.Equal(other.Token, actual.CancellationToken);
    }
}
