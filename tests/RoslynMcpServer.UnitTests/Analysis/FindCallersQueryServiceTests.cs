using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Application.Analysis;
using RoslynMcpServer.Application.Analysis.Operations;

namespace RoslynMcpServer.UnitTests.Analysis;

public sealed class FindCallersQueryServiceTests
{
    [Fact]
    public async Task FindCallersAsync_PassesSuccessfulPayloadThroughUnchanged()
    {
        var expected = new FindCallersResult(CreateSymbol(),
            [new CallerDescriptor("Sample.Caller()", new("File.cs", 2, 5), "Run();")], null, null);
        var provider = new TestProvider { NextResult = expected };

        var result = await CreateService(provider).FindCallersAsync(CreateRequest());

        Assert.Same(expected, result);
        Assert.Single(result.Callers);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task FindCallersAsync_MapsFailure_PreservingSymbolAndDroppingCallers()
    {
        var symbol = CreateSymbol();
        var provider = new TestProvider
        {
            NextResult = new(symbol, [new("Partial", new("File.cs", 2, 5), null)], "Lookup failed.", "Retry callers.")
        };

        var result = await CreateService(provider).FindCallersAsync(CreateRequest());

        Assert.Same(symbol, result.Symbol);
        Assert.Empty(result.Callers);
        Assert.IsAssignableFrom<IReadOnlyList<CallerDescriptor>>(result.Callers);
        Assert.Equal("Lookup failed.", result.FailureReason);
        Assert.Equal("Retry callers.", result.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task FindCallersAsync_ProjectsValidationFailure_WithoutCallingProvider()
    {
        var provider = new TestProvider();

        var result = await CreateService(provider).FindCallersAsync(CreateRequest() with { MaxResults = 0 });

        Assert.Null(result.Symbol);
        Assert.Empty(result.Callers);
        Assert.Equal("MaxResults must be a positive value.", result.FailureReason);
        Assert.Equal("Pass a positive integer (e.g., the default of 100).", result.Guidance);
        Assert.Equal(0, provider.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FindCallersAsync_ProjectsCancellation_WithStablePublicPayload(bool cancelInFlight)
    {
        using var source = new CancellationTokenSource();
        if (!cancelInFlight)
        {
            await source.CancelAsync();
        }
        var provider = new TestProvider { CancelOnCall = cancelInFlight ? source : null };

        var result = await CreateService(provider).FindCallersAsync(CreateRequest(), source.Token);

        Assert.Null(result.Symbol);
        Assert.Empty(result.Callers);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
        Assert.Equal(cancelInFlight ? 1 : 0, provider.Calls);
    }

    private static FindCallersQueryService CreateService(TestProvider provider) =>
        new(new FindCallersQueryOperation(provider, NullLogger<FindCallersQueryOperation>.Instance));

    private static FindCallersRequest CreateRequest() => new("Sample.sln", "File.cs", 1, 1);

    private static SymbolDescriptor CreateSymbol() => new("Run", "Method", "Sample.Run()", "Sample", new("File.cs", 1, 1));

    private sealed class TestProvider : IFindCallersQueryDataProvider
    {
        public int Calls { get; private set; }
        public FindCallersResult NextResult { get; init; } = new(null, [], null, null);
        public CancellationTokenSource? CancelOnCall { get; init; }

        public async ValueTask<FindCallersResult> FindCallersAsync(FindCallersRequest request, CancellationToken cancellationToken = default)
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
