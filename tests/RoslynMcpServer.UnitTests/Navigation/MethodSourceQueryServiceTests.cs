using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class MethodSourceQueryServiceTests
{
    [Fact]
    public async Task GetMethodSourceAsync_PassesThroughSuccessPayload()
    {
        var signature = Signature();
        var method = new MethodSourceDescriptor("declaration.cs",
            new SymbolLocation("declaration.cs", 3, 5), new SymbolLocation("declaration.cs", 3, 26),
            "public void Run() { }");
        var expected = new GetMethodSourceResult(signature, method, null, null);

        var result = await CreateService(expected).GetMethodSourceAsync(ValidRequest());

        Assert.Same(expected, result);
        Assert.Same(signature, result.Signature);
        Assert.Same(method, result.Method);
    }

    [Fact]
    public async Task GetMethodSourceAsync_PreservesSignature_WhenSourceUnavailable()
    {
        var signature = Signature();
        var expected = new GetMethodSourceResult(signature, null,
            "The resolved callable member does not have source syntax available.",
            "Use a callable member declared in a source document of the loaded solution.");

        var result = await CreateService(expected).GetMethodSourceAsync(ValidRequest());

        Assert.Same(signature, result.Signature);
        Assert.Null(result.Method);
        Assert.Equal(expected.FailureReason, result.FailureReason);
        Assert.Equal(expected.Guidance, result.Guidance);
    }

    [Fact]
    public async Task GetMethodSourceAsync_MapsFailureWithoutSignature()
    {
        var result = await CreateService(new GetMethodSourceResult(null, null, "Provider failed.", "Retry."))
            .GetMethodSourceAsync(ValidRequest());

        Assert.Null(result.Signature);
        Assert.Null(result.Method);
        Assert.Equal("Provider failed.", result.FailureReason);
        Assert.Equal("Retry.", result.Guidance);
    }

    [Fact]
    public async Task GetMethodSourceAsync_MapsCanceledResult_ToStableCancellationPayload()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await CreateService(new GetMethodSourceResult(null, null, null, null))
            .GetMethodSourceAsync(ValidRequest(), cts.Token);

        Assert.Null(result.Signature);
        Assert.Null(result.Method);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
    }

    private static MethodSourceQueryService CreateService(GetMethodSourceResult result) =>
        new(new MethodSourceQueryOperation(new TestProvider(result), NullLogger<MethodSourceQueryOperation>.Instance));

    private static GetMethodSourceRequest ValidRequest() => new("sample.sln", "source.cs", 1, 1);

    private static MethodSignatureDescriptor Signature() =>
        new("Run", "Method", "Example.Run()", "Public", false, false, "void", "Example",
            Array.Empty<MethodParameterDescriptor>(), null);

    private sealed class TestProvider(GetMethodSourceResult result) : IMethodSourceQueryDataProvider
    {
        public ValueTask<GetMethodSourceResult> GetMethodSourceAsync(
            GetMethodSourceRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }
}
