using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Application.Analysis;
using RoslynMcpServer.Application.Analysis.Operations;

namespace RoslynMcpServer.UnitTests.Analysis;

public sealed class OutgoingCallsQueryServiceTests
{
    [Fact]
    public async Task GetOutgoingCallsAsync_PassesSuccessfulPayloadThroughUnchanged()
    {
        var expected = new GetOutgoingCallsResult(CreateSymbol(),
            [new CallEdgeDescriptor(CreateSymbol(), new("File.cs", 2, 5), "Run();")], null, null);
        var provider = new TestProvider { NextResult = expected };

        var result = await CreateService(provider).GetOutgoingCallsAsync(CreateRequest());

        Assert.Same(expected, result);
        Assert.Single(result.Calls);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task GetOutgoingCallsAsync_MapsFailure_PreservingSymbolAndDroppingCalls()
    {
        var symbol = CreateSymbol();
        var provider = new TestProvider
        {
            NextResult = new(symbol, [new(CreateSymbol(), new("File.cs", 2, 5), null)], "Lookup failed.", "Retry outgoing calls.")
        };

        var result = await CreateService(provider).GetOutgoingCallsAsync(CreateRequest());

        Assert.Same(symbol, result.Symbol);
        Assert.Empty(result.Calls);
        Assert.IsAssignableFrom<IReadOnlyList<CallEdgeDescriptor>>(result.Calls);
        Assert.Equal("Lookup failed.", result.FailureReason);
        Assert.Equal("Retry outgoing calls.", result.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task GetOutgoingCallsAsync_ProjectsValidationFailure_WithoutCallingProvider()
    {
        var provider = new TestProvider();

        var result = await CreateService(provider).GetOutgoingCallsAsync(CreateRequest() with { MaxResults = 0 });

        Assert.Null(result.Symbol);
        Assert.Empty(result.Calls);
        Assert.Equal("MaxResults must be a positive value.", result.FailureReason);
        Assert.Equal("Pass a positive integer (e.g., the default of 100).", result.Guidance);
        Assert.Equal(0, provider.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetOutgoingCallsAsync_ProjectsCancellation_WithStablePublicPayload(bool cancelInFlight)
    {
        using var source = new CancellationTokenSource();
        if (!cancelInFlight)
        {
            await source.CancelAsync();
        }
        var provider = new TestProvider { CancelOnCall = cancelInFlight ? source : null };

        var result = await CreateService(provider).GetOutgoingCallsAsync(CreateRequest(), source.Token);

        Assert.Null(result.Symbol);
        Assert.Empty(result.Calls);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
        Assert.Equal(cancelInFlight ? 1 : 0, provider.Calls);
    }

    private static OutgoingCallsQueryService CreateService(TestProvider provider) =>
        new(new OutgoingCallsQueryOperation(provider, NullLogger<OutgoingCallsQueryOperation>.Instance));

    private static GetOutgoingCallsRequest CreateRequest() => new("Sample.sln", "File.cs", 1, 1);

    private static SymbolDescriptor CreateSymbol() => new("Run", "Method", "Sample.Run()", "Sample", new("File.cs", 1, 1));

    private sealed class TestProvider : IOutgoingCallsQueryDataProvider
    {
        public int Calls { get; private set; }
        public GetOutgoingCallsResult NextResult { get; init; } = new(null, [], null, null);
        public CancellationTokenSource? CancelOnCall { get; init; }

        public async ValueTask<GetOutgoingCallsResult> GetOutgoingCallsAsync(GetOutgoingCallsRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (CancelOnCall is not null)
            {
                await CancelOnCall.CancelAsync();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return NextResult;
        }
    }
}
