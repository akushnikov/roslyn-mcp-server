using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Application.Analysis;
using RoslynMcpServer.Application.Analysis.Operations;

namespace RoslynMcpServer.UnitTests.Analysis;

public sealed class ChangeImpactQueryServiceTests
{
    [Fact]
    public async Task AnalyzeChangeImpactAsync_PassesSuccessfulPayloadThroughUnchanged()
    {
        var expected = new AnalyzeChangeImpactResult(CreateSymbol(),
            CreateImpact(), null, null);
        var provider = new TestProvider { NextResult = expected };

        var result = await CreateService(provider).AnalyzeChangeImpactAsync(CreateRequest());

        Assert.Same(expected, result);
        Assert.NotNull(result.Impact);
        Assert.Equal(1, result.Impact.CallerCount);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task AnalyzeChangeImpactAsync_MapsFailure_PreservingSymbolAndDroppingCallers()
    {
        var symbol = CreateSymbol();
        var provider = new TestProvider
        {
            NextResult = new(symbol, CreateImpact(), "Lookup failed.", "Retry impact.")
        };

        var result = await CreateService(provider).AnalyzeChangeImpactAsync(CreateRequest());

        Assert.Same(symbol, result.Symbol);
        Assert.Null(result.Impact);
        Assert.Equal("Lookup failed.", result.FailureReason);
        Assert.Equal("Retry impact.", result.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task AnalyzeChangeImpactAsync_ProjectsValidationFailure_WithoutCallingProvider()
    {
        var provider = new TestProvider();

        var result = await CreateService(provider).AnalyzeChangeImpactAsync(CreateRequest() with { MaxResults = 0 });

        Assert.Null(result.Symbol);
        Assert.Empty(result.Callers);
        Assert.Equal("MaxResults must be a positive value.", result.FailureReason);
        Assert.Equal("Pass a positive integer (e.g., the default of 100).", result.Guidance);
        Assert.Equal(0, provider.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnalyzeChangeImpactAsync_ProjectsCancellation_WithStablePublicPayload(bool cancelInFlight)
    {
        using var source = new CancellationTokenSource();
        if (!cancelInFlight)
        {
            await source.CancelAsync();
        }
        var provider = new TestProvider { CancelOnCall = cancelInFlight ? source : null };

        var result = await CreateService(provider).AnalyzeChangeImpactAsync(CreateRequest(), source.Token);

        Assert.Null(result.Symbol);
        Assert.Empty(result.Callers);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
        Assert.Equal(cancelInFlight ? 1 : 0, provider.Calls);
    }

    private static ChangeImpactQueryService CreateService(TestProvider provider) =>
        new(new ChangeImpactQueryOperation(provider, NullLogger<ChangeImpactQueryOperation>.Instance));

    private static AnalyzeChangeImpactRequest CreateRequest() => new("Sample.sln", "File.cs", 1, 1);

    private static SymbolDescriptor CreateSymbol() => new("Run", "Method", "Sample.Run()", "Sample", new("File.cs", 1, 1));

    private static ChangeImpactDescriptor CreateImpact() => new(1, 1, 0,
        ["File.cs"],
        [new(new("File.cs", 2, 5), null, "Run();")],
        [new("Sample.Caller()", new("File.cs", 2, 5), "Run();")], []);

    private sealed class TestProvider : IChangeImpactQueryDataProvider
    {
        public int Calls { get; private set; }
        public AnalyzeChangeImpactResult NextResult { get; init; } = new(null, null, null, null);
        public CancellationTokenSource? CancelOnCall { get; init; }

        public async ValueTask<AnalyzeChangeImpactResult> AnalyzeChangeImpactAsync(AnalyzeChangeImpactRequest request, CancellationToken cancellationToken = default)
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
