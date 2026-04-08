using OneOf;
using OneOf.Types;
using RoslynMcpServer.Abstractions.QueryPipeline.Models;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.UnitTests.QueryPipeline;

public sealed class QueryOperationBaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsCanceledBeforePipeline_WhenCancellationAlreadyRequested()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var operation = new TestQueryOperation();

        var result = await operation.ExecuteAsync(new TestRequest("request"), cts.Token);
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);

        Assert.Empty(operation.StageCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceled_WhenCancellationRequestedDuringCore()
    {
        using var cts = new CancellationTokenSource();
        var operation = new TestQueryOperation
        {
            CancelDuringCore = true,
            OnValidate = _ => cts.Cancel()
        };

        var result = await operation.ExecuteAsync(new TestRequest("request"), cts.Token);
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);

        Assert.Collection(
            operation.StageCalls,
            stage => Assert.Equal("validate", stage),
            stage => Assert.Equal("core", stage));
    }

    [Fact]
    public async Task ExecuteAsync_MapsUnhandledCoreError_WithDeterministicStageOrder()
    {
        var operation = new TestQueryOperation
        {
            CoreException = new InvalidOperationException("sensitive backend details")
        };

        var result = await operation.ExecuteAsync(new TestRequest("request"));
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal("Mapped failure", error.Value.Value.FailureReason);
        Assert.Equal("Retry request.", error.Value.Value.Guidance);
        Assert.IsType<InvalidOperationException>(operation.MappedException);

        Assert.Collection(
            operation.StageCalls,
            stage => Assert.Equal("validate", stage),
            stage => Assert.Equal("core", stage),
            stage => Assert.Equal("map", stage));
    }

    [Fact]
    public async Task ExecuteAsync_RunsValidationCoreThenCompletionHook_OnSuccess()
    {
        var expected = new TestResult("ok");
        var operation = new TestQueryOperation
        {
            CoreResult = expected
        };

        var result = await operation.ExecuteAsync(new TestRequest("request"));
        var (success, error, canceled) = result;

        Assert.NotNull(success);
        Assert.Same(expected, success.Value.Value);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Collection(
            operation.StageCalls,
            stage => Assert.Equal("validate", stage),
            stage => Assert.Equal("core", stage));
    }

    private sealed record TestRequest(string Input);

    private sealed record TestResult(string? Value);

    private sealed class TestQueryOperation : QueryOperationBase<TestRequest, TestResult, QueryError>
    {
        public List<string> StageCalls { get; } = [];

        public Exception? CoreException { get; set; }

        public TestResult CoreResult { get; init; } = new("ok");

        public Exception? MappedException { get; private set; }

        public bool CancelDuringCore { get; init; }

        public Action<CancellationToken>? OnValidate { get; set; }

        protected override Error<QueryError>? Validate(TestRequest request)
        {
            StageCalls.Add("validate");
            OnValidate?.Invoke(CancellationToken.None);
            return null;
        }

        protected override ValueTask<OneOf<Success<TestResult>, Error<QueryError>, Canceled>> ExecuteCoreAsync(
            TestRequest request,
            CancellationToken cancellationToken)
        {
            StageCalls.Add("core");
            cancellationToken.ThrowIfCancellationRequested();

            if (CancelDuringCore)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (CoreException is not null)
            {
                throw CoreException;
            }

            return ValueTask.FromResult<OneOf<Success<TestResult>, Error<QueryError>, Canceled>>(
                new Success<TestResult>(CoreResult));
        }

        protected override Error<QueryError> MapUnhandledError(TestRequest request, Exception exception)
        {
            StageCalls.Add("map");
            MappedException = exception;
            return new Error<QueryError>(new QueryError("Mapped failure", "Retry request."));
        }
    }
}
