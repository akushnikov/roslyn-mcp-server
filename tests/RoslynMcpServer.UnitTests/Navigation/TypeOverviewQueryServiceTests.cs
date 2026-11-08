using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class TypeOverviewQueryServiceTests
{
    [Fact]
    public async Task GetTypeOverviewAsync_PassesThroughSuccessPayload()
    {
        var type = new SymbolDescriptor("Example", "Class", "Sample.Example", "Sample", null);
        var baseType = new SymbolDescriptor("Base", "Class", "Sample.Base", "Sample", null);
        var directInterface = new SymbolDescriptor("IExample", "Interface", "Sample.IExample", "Sample", null);
        var member = new TypeMemberDescriptor("Run", "Method", "Sample.Example.Run()", "Public", false, null);
        var expected = new GetTypeOverviewResult(type, baseType, new[] { directInterface }, new[] { member }, null, null);

        var result = await CreateService(expected).GetTypeOverviewAsync(CreateValidRequest());

        Assert.Same(expected, result);
        Assert.Same(type, result.Type);
        Assert.Same(baseType, result.BaseType);
        Assert.Same(directInterface, Assert.Single(result.Interfaces));
        Assert.Same(member, Assert.Single(result.Members));
    }

    [Fact]
    public async Task GetTypeOverviewAsync_PreservesResolvedType_InContextualFailure()
    {
        var symbol = new SymbolDescriptor("Sample", "Namespace", "Sample", null, null);
        var expected = new GetTypeOverviewResult(symbol, null, Array.Empty<SymbolDescriptor>(),
            Array.Empty<TypeMemberDescriptor>(), "The resolved symbol does not belong to a named type.",
            "Use a type declaration or a member inside a named type.");

        var result = await CreateService(expected).GetTypeOverviewAsync(CreateValidRequest());

        Assert.Same(symbol, result.Type);
        Assert.Null(result.BaseType);
        Assert.Empty(result.Interfaces);
        Assert.Empty(result.Members);
        Assert.Equal(expected.FailureReason, result.FailureReason);
        Assert.Equal(expected.Guidance, result.Guidance);
    }

    [Fact]
    public async Task GetTypeOverviewAsync_MapsProviderFailure_WithoutResolvedType()
    {
        var expected = new GetTypeOverviewResult(null, null, Array.Empty<SymbolDescriptor>(),
            Array.Empty<TypeMemberDescriptor>(), "The requested solution is not currently loaded.",
            "Call load_solution first for the requested solutionPath.");

        var result = await CreateService(expected).GetTypeOverviewAsync(CreateValidRequest());

        Assert.Null(result.Type);
        Assert.Null(result.BaseType);
        Assert.Empty(result.Interfaces);
        Assert.Empty(result.Members);
        Assert.Equal(expected.FailureReason, result.FailureReason);
        Assert.Equal(expected.Guidance, result.Guidance);
    }

    [Fact]
    public async Task GetTypeOverviewAsync_MapsCanceledResult_ToStableCancellationPayload()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await CreateService(new GetTypeOverviewResult(null, null,
                Array.Empty<SymbolDescriptor>(), Array.Empty<TypeMemberDescriptor>(), null, null))
            .GetTypeOverviewAsync(CreateValidRequest(), cts.Token);

        Assert.Null(result.Type);
        Assert.Null(result.BaseType);
        Assert.Empty(result.Interfaces);
        Assert.Empty(result.Members);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
    }

    private static TypeOverviewQueryService CreateService(GetTypeOverviewResult result) =>
        new(new TypeOverviewQueryOperation(new TestTypeOverviewQueryDataProvider(result),
            NullLogger<TypeOverviewQueryOperation>.Instance));

    private static GetTypeOverviewRequest CreateValidRequest() =>
        new(@"C:\repo\Sample.sln", @"C:\repo\src\File.cs", 1, 1);

    private sealed class TestTypeOverviewQueryDataProvider(GetTypeOverviewResult result) : ITypeOverviewQueryDataProvider
    {
        public ValueTask<GetTypeOverviewResult> GetTypeOverviewAsync(
            GetTypeOverviewRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }
}
