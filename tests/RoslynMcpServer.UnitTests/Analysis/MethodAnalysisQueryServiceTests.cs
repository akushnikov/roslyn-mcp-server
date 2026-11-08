using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Application.Analysis;
using RoslynMcpServer.Application.Analysis.Operations;

namespace RoslynMcpServer.UnitTests.Analysis;

public sealed class MethodAnalysisQueryServiceTests
{
    [Fact]
    public async Task AnalyzeMethodAsync_ReturnsSuccessfulPayloadUnchanged()
    {
        var expected = Payload();
        var provider = new TestProvider { Result = expected };
        var actual = await Service(provider).AnalyzeMethodAsync(Request());
        Assert.Same(expected, actual);
        Assert.NotNull(actual.DataFlow);
        Assert.NotNull(actual.ControlFlow);
        Assert.Single(actual.OutgoingCalls);
    }

    [Fact]
    public async Task AnalyzeMethodAsync_PreservesSignatureButClearsPartialPayloadOnError()
    {
        var partial = Payload() with { FailureReason = "No flow.", Guidance = "Use block." };
        var provider = new TestProvider { Result = partial };
        var actual = await Service(provider).AnalyzeMethodAsync(Request());
        Assert.Same(partial.Signature, actual.Signature);
        Assert.Null(actual.DataFlow);
        Assert.Null(actual.ControlFlow);
        Assert.Empty(actual.OutgoingCalls);
        Assert.Equal(partial.FailureReason, actual.FailureReason);
        Assert.Equal(partial.Guidance, actual.Guidance);
    }

    [Fact]
    public async Task AnalyzeMethodAsync_ProjectsValidationBeforeCallingProvider()
    {
        var provider = new TestProvider();
        var actual = await Service(provider).AnalyzeMethodAsync(Request() with { MaxOutgoingCalls = 0 });
        Assert.Null(actual.Signature);
        Assert.Null(actual.DataFlow);
        Assert.Null(actual.ControlFlow);
        Assert.Empty(actual.OutgoingCalls);
        Assert.Equal("MaxOutgoingCalls must be a positive value.", actual.FailureReason);
        Assert.Equal(0, provider.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnalyzeMethodAsync_ProjectsStableCancellation(bool inFlight)
    {
        using var source = new CancellationTokenSource();
        if (!inFlight) await source.CancelAsync();
        var provider = new TestProvider { CancelOnCall = inFlight ? source : null };
        var actual = await Service(provider).AnalyzeMethodAsync(Request(), source.Token);
        Assert.Null(actual.Signature);
        Assert.Null(actual.DataFlow);
        Assert.Null(actual.ControlFlow);
        Assert.Empty(actual.OutgoingCalls);
        Assert.Equal("The operation was canceled.", actual.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", actual.Guidance);
        Assert.Equal(inFlight ? 1 : 0, provider.Calls);
    }

    private static MethodAnalysisQueryService Service(TestProvider provider) => new(new(provider, NullLogger<MethodAnalysisQueryOperation>.Instance));
    private static AnalyzeMethodRequest Request() => new("Sample.sln", "File.cs", 1, 1);
    private static AnalyzeMethodResult Payload() => new(new MethodSignatureDescriptor("Run", "Method", "Sample.Run()", "Public", false, false, "int", "Sample", [], new("File.cs", 1, 1)), new([], [], [], [], [], [], [], [], [], true), new(true, false, [], [], [], true), [new(null, new("File.cs", 2, 1), "Next()")], null, null);

    private sealed class TestProvider : IMethodAnalysisQueryDataProvider
    {
        public int Calls { get; private set; }
        public AnalyzeMethodResult Result { get; init; } = Payload();
        public CancellationTokenSource? CancelOnCall { get; init; }

        public async ValueTask<AnalyzeMethodResult> AnalyzeMethodAsync(AnalyzeMethodRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (CancelOnCall is not null) await CancelOnCall.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return Result;
        }
    }
}
