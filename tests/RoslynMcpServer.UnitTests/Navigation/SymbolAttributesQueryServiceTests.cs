using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class SymbolAttributesQueryServiceTests
{
    [Fact]
    public async Task GetSymbolAttributesAsync_PassesThroughSuccessPayload_WithoutReorderingAttributes()
    {
        var symbol = new SymbolDescriptor("Example", "Class", "Sample.Example", "Sample", null);
        var attributes = new[]
        {
            new SymbolAttributeDescriptor("Zeta", "Sample.ZetaAttribute", new[] { "[1, null]" }, new[] { "Name = demo" }),
            new SymbolAttributeDescriptor("Alpha", "Sample.AlphaAttribute", Array.Empty<string>(), Array.Empty<string>())
        };
        var expected = new GetSymbolAttributesResult(symbol, attributes, null, null);

        var result = await CreateService(expected).GetSymbolAttributesAsync(ValidRequest());

        Assert.Same(expected, result);
        Assert.Same(symbol, result.Symbol);
        Assert.Same(attributes, result.Attributes);
        Assert.Equal("[1, null]", result.Attributes[0].ConstructorArguments[0]);
        Assert.Equal("Name = demo", result.Attributes[0].NamedArguments[0]);
    }

    [Fact]
    public async Task GetSymbolAttributesAsync_PreservesSymbolAndFailureContext()
    {
        var symbol = new SymbolDescriptor("Example", "Class", "Sample.Example", "Sample", null);
        var expected = new GetSymbolAttributesResult(symbol, Array.Empty<SymbolAttributeDescriptor>(), "Provider failed.", "Retry lookup.");

        var result = await CreateService(expected).GetSymbolAttributesAsync(ValidRequest());

        Assert.Same(symbol, result.Symbol);
        Assert.Empty(result.Attributes);
        Assert.Equal(expected.FailureReason, result.FailureReason);
        Assert.Equal(expected.Guidance, result.Guidance);
    }

    [Fact]
    public async Task GetSymbolAttributesAsync_MapsValidationFailure_ToPublicResult()
    {
        var result = await CreateService(new(null, Array.Empty<SymbolAttributeDescriptor>(), null, null))
            .GetSymbolAttributesAsync(ValidRequest() with { SolutionPath = "" });

        Assert.Null(result.Symbol);
        Assert.Empty(result.Attributes);
        Assert.Equal("A non-empty solutionPath is required.", result.FailureReason);
        Assert.NotNull(result.Guidance);
    }

    [Fact]
    public async Task GetSymbolAttributesAsync_MapsCanceled_ToStablePayload()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await CreateService(new(null, Array.Empty<SymbolAttributeDescriptor>(), null, null))
            .GetSymbolAttributesAsync(ValidRequest(), cts.Token);

        Assert.Null(result.Symbol);
        Assert.Empty(result.Attributes);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
    }

    private static SymbolAttributesQueryService CreateService(GetSymbolAttributesResult nextResult) =>
        new(new SymbolAttributesQueryOperation(new TestProvider(nextResult), NullLogger<SymbolAttributesQueryOperation>.Instance));

    private static GetSymbolAttributesRequest ValidRequest() => new("sample.sln", "source.cs", 1, 1);

    private sealed class TestProvider(GetSymbolAttributesResult result) : ISymbolAttributesQueryDataProvider
    {
        public ValueTask<GetSymbolAttributesResult> GetSymbolAttributesAsync(
            GetSymbolAttributesRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }
}
