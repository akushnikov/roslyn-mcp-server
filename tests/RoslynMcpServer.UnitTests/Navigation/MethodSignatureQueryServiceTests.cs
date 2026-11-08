using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class MethodSignatureQueryServiceTests
{
    [Fact]
    public async Task GetMethodSignatureAsync_PassesThroughSuccessfulSignature()
    {
        var signature = new MethodSignatureDescriptor(
            "get_Name", "PropertyGet", "Sample.Name.get", "Public", false, false,
            "string", "Sample", Array.Empty<MethodParameterDescriptor>(), null);
        var expected = new GetMethodSignatureResult(signature, null, null);

        var result = await CreateService(new TestMethodSignatureQueryDataProvider { NextResult = expected })
            .GetMethodSignatureAsync(CreateValidRequest());

        Assert.Same(expected, result);
        Assert.Same(signature, result.Signature);
        Assert.Null(result.FailureReason);
        Assert.Null(result.Guidance);
    }

    [Theory]
    [InlineData("The resolved symbol is not a callable member.", "Use a method, constructor, local function, accessor, property, or event declaration.")]
    [InlineData("The requested solution is not currently loaded.", "Call load_solution first for the requested solutionPath.")]
    [InlineData("Provider failed.", null)]
    public async Task GetMethodSignatureAsync_PreservesStructuredFailure(string failureReason, string? guidance)
    {
        var provider = new TestMethodSignatureQueryDataProvider
        {
            NextResult = new GetMethodSignatureResult(null, failureReason, guidance)
        };

        var result = await CreateService(provider).GetMethodSignatureAsync(CreateValidRequest());

        Assert.Null(result.Signature);
        Assert.Equal(failureReason, result.FailureReason);
        Assert.Equal(guidance, result.Guidance);
    }

    [Fact]
    public async Task GetMethodSignatureAsync_MapsCanceledOutcome_ToStablePayload()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var provider = new TestMethodSignatureQueryDataProvider();

        var result = await CreateService(provider).GetMethodSignatureAsync(CreateValidRequest(), cts.Token);

        Assert.Null(result.Signature);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task GetMethodSignatureAsync_ProjectsValidationFailure_WithoutProviderCall()
    {
        var provider = new TestMethodSignatureQueryDataProvider();

        var result = await CreateService(provider)
            .GetMethodSignatureAsync(CreateValidRequest() with { FilePath = "" });

        Assert.Null(result.Signature);
        Assert.Equal("A non-empty filePath is required.", result.FailureReason);
        Assert.Equal(0, provider.Calls);
    }

    private static MethodSignatureQueryService CreateService(TestMethodSignatureQueryDataProvider provider) =>
        new(new MethodSignatureQueryOperation(provider, NullLogger<MethodSignatureQueryOperation>.Instance));

    private static GetMethodSignatureRequest CreateValidRequest() =>
        new(@"C:\repo\Sample.sln", @"C:\repo\File.cs", 1, 1);

    private sealed class TestMethodSignatureQueryDataProvider : IMethodSignatureQueryDataProvider
    {
        public int Calls { get; private set; }
        public GetMethodSignatureResult NextResult { get; init; } = new(null, null, null);

        public ValueTask<GetMethodSignatureResult> GetMethodSignatureAsync(
            GetMethodSignatureRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(NextResult);
        }
    }
}
