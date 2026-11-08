using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Application.Analysis;
using RoslynMcpServer.Application.Analysis.Operations;

namespace RoslynMcpServer.UnitTests.Analysis;

public sealed class TypeCompatibilityQueryServiceTests
{
    [Fact]
    public async Task CheckTypeCompatibilityAsync_PassesSuccessfulResultThroughUnchanged()
    {
        var expected = new CheckTypeCompatibilityResult(Symbol("Source"), Symbol("Target"),
            new("int", "long", false, true, true, false, false, false, true, false), null, null);
        var provider = new TestProvider { Result = expected };
        var result = await Service(provider).CheckTypeCompatibilityAsync(Request());

        Assert.Same(expected, result);
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckTypeCompatibilityAsync_PreservesResolvedContextAndDropsPartialCompatibility(bool hasTarget)
    {
        var source = Symbol("Source");
        var target = hasTarget ? Symbol("Target") : null;
        var provider = new TestProvider { Result = new(source, target,
            new("int", "long", false, true, true, false, false, false, true, false), "Lookup failed.", "Retry lookup.") };
        var result = await Service(provider).CheckTypeCompatibilityAsync(Request());

        Assert.Same(source, result.Source);
        Assert.Same(target, result.Target);
        Assert.Null(result.Compatibility);
        Assert.Equal("Lookup failed.", result.FailureReason);
        Assert.Equal("Retry lookup.", result.Guidance);
    }

    [Fact]
    public async Task CheckTypeCompatibilityAsync_ProjectsValidationBeforeProvider()
    {
        var provider = new TestProvider();
        var result = await Service(provider).CheckTypeCompatibilityAsync(Request() with { SourceLine = 0 });

        Assert.Null(result.Source);
        Assert.Null(result.Target);
        Assert.Null(result.Compatibility);
        Assert.Equal("Source line and column must be positive 1-based values.", result.FailureReason);
        Assert.Equal(0, provider.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckTypeCompatibilityAsync_CancellationHasStableEmptyPublicPayload(bool inFlight)
    {
        using var cancellation = new CancellationTokenSource();
        if (!inFlight) await cancellation.CancelAsync();
        var provider = new TestProvider { CancelOnCall = inFlight ? cancellation : null };
        var result = await Service(provider).CheckTypeCompatibilityAsync(Request(), cancellation.Token);

        Assert.Null(result.Source);
        Assert.Null(result.Target);
        Assert.Null(result.Compatibility);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
        Assert.Equal(inFlight ? 1 : 0, provider.Calls);
    }

    private static TypeCompatibilityQueryService Service(TestProvider provider) =>
        new(new(provider, NullLogger<TypeCompatibilityQueryOperation>.Instance));
    private static CheckTypeCompatibilityRequest Request() => new("Sample.sln", "Source.cs", 1, 1, "Target.cs", 1, 1);
    private static SymbolDescriptor Symbol(string name) => new(name, "Class", name, "Sample", new("File.cs", 1, 1));

    private sealed class TestProvider : ITypeCompatibilityQueryDataProvider
    {
        public int Calls { get; private set; }
        public CheckTypeCompatibilityResult Result { get; init; } = new(null, null, null, null, null);
        public CancellationTokenSource? CancelOnCall { get; init; }

        public async ValueTask<CheckTypeCompatibilityResult> CheckTypeCompatibilityAsync(CheckTypeCompatibilityRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            if (CancelOnCall is not null) await CancelOnCall.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return Result;
        }
    }
}
