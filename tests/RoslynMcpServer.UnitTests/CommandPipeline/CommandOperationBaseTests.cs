using OneOf;
using OneOf.Types;
using RoslynMcpServer.Abstractions.CommandPipeline.Models;
using RoslynMcpServer.Application.CommandPipeline;

namespace RoslynMcpServer.UnitTests.CommandPipeline;

public sealed class CommandOperationBaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsCanceledBeforePipeline_WhenCancellationAlreadyRequested()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var operation = new TestCommandOperation();

        var result = await operation.ExecuteAsync(new TestRequest("request"), cts.Token);
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Empty(operation.StageCalls);
    }

    [Fact]
    public async Task ExecuteAsync_MapsUnhandledCoreError_WithDeterministicStageOrder()
    {
        var operation = new TestCommandOperation
        {
            CoreException = new InvalidOperationException("boom")
        };

        var result = await operation.ExecuteAsync(new TestRequest("request"));
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal("Mapped failure", error.Value.Value.FailureReason);
        Assert.Equal("Retry command.", error.Value.Value.Guidance);
        Assert.Collection(
            operation.StageCalls,
            stage => Assert.Equal("validate", stage),
            stage => Assert.Equal("core", stage),
            stage => Assert.Equal("map", stage));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenCoreCompletes()
    {
        var expected = new TestResult("ok");
        var operation = new TestCommandOperation
        {
            CoreResult = expected
        };

        var result = await operation.ExecuteAsync(new TestRequest("request"));
        var (success, error, canceled) = result;

        Assert.NotNull(success);
        Assert.Same(expected, success.Value.Value);
        Assert.Null(error);
        Assert.Null(canceled);
    }

    private sealed record TestRequest(string Input);

    private sealed record TestResult(string? Value);

    private sealed class TestCommandOperation : CommandOperationBase<TestRequest, TestResult, CommandError>
    {
        public List<string> StageCalls { get; } = [];

        public Exception? CoreException { get; set; }

        public TestResult CoreResult { get; init; } = new("ok");

        protected override Error<CommandError>? Validate(TestRequest request)
        {
            StageCalls.Add("validate");
            return null;
        }

        protected override ValueTask<OneOf<Success<TestResult>, Error<CommandError>, Canceled>> ExecuteCoreAsync(
            TestRequest request,
            CancellationToken cancellationToken)
        {
            StageCalls.Add("core");
            cancellationToken.ThrowIfCancellationRequested();

            if (CoreException is not null)
            {
                throw CoreException;
            }

            return ValueTask.FromResult<OneOf<Success<TestResult>, Error<CommandError>, Canceled>>(
                new Success<TestResult>(CoreResult));
        }

        protected override Error<CommandError> MapUnhandledError(TestRequest request, Exception exception)
        {
            StageCalls.Add("map");
            return new Error<CommandError>(new CommandError("Mapped failure", "Retry command."));
        }
    }
}
