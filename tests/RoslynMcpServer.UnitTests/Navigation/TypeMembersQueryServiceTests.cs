using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class TypeMembersQueryServiceTests
{
    [Fact]
    public async Task GetTypeMembersAsync_PassesThroughSuccessPayload()
    {
        var type = new SymbolDescriptor("Example", "Class", "Sample.Example", "Sample", null);
        var member = new TypeMemberDescriptor("Run", "Method", "Sample.Example.Run()", "Public", false, null);
        var expected = new GetTypeMembersResult(type, new[] { member }, null, null);
        var service = new TypeMembersQueryService(new TypeMembersQueryOperation(
            new TestTypeMembersQueryDataProvider { NextResult = expected },
            NullLogger<TypeMembersQueryOperation>.Instance));

        var result = await service.GetTypeMembersAsync(CreateValidRequest());

        Assert.Same(expected, result);
        Assert.Same(type, result.Type);
        Assert.Same(member, Assert.Single(result.Members));
    }

    [Fact]
    public async Task GetTypeMembersAsync_PreservesResolvedSymbol_WhenNoSourceDefinitionExists()
    {
        var symbol = new SymbolDescriptor("WriteLine", "Method", "System.Console.WriteLine()", "System.Console", null);
        var expected = new GetTypeMembersResult(
            symbol,
            Array.Empty<TypeMemberDescriptor>(),
            "No source definition was found for the resolved symbol.",
            "The symbol may come from metadata or generated code outside the loaded solution.");
        var service = new TypeMembersQueryService(
            new TypeMembersQueryOperation(
                new TestTypeMembersQueryDataProvider { NextResult = expected },
                NullLogger<TypeMembersQueryOperation>.Instance));

        var result = await service.GetTypeMembersAsync(CreateValidRequest());

        Assert.Same(symbol, result.Type);
        Assert.Empty(result.Members);
        Assert.Equal(expected.FailureReason, result.FailureReason);
        Assert.Equal(expected.Guidance, result.Guidance);
    }

    [Fact]
    public async Task GetTypeMembersAsync_MapsCanceledResult_ToStableCancellationPayload()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var service = new TypeMembersQueryService(
            new TypeMembersQueryOperation(
                new TestTypeMembersQueryDataProvider(),
                NullLogger<TypeMembersQueryOperation>.Instance));

        var result = await service.GetTypeMembersAsync(CreateValidRequest(), cts.Token);

        Assert.Null(result.Type);
        Assert.Empty(result.Members);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
    }

    [Fact]
    public async Task GetTypeMembersAsync_MapsProviderFailure_ToFailurePayload()
    {
        var service = new TypeMembersQueryService(
            new TypeMembersQueryOperation(
                new TestTypeMembersQueryDataProvider
                {
                    NextResult = new GetTypeMembersResult(
                        Type: null,
                        Members: Array.Empty<TypeMemberDescriptor>(),
                        FailureReason: "Provider failed.",
                        Guidance: "Retry definition lookup.")
                },
                NullLogger<TypeMembersQueryOperation>.Instance));

        var result = await service.GetTypeMembersAsync(CreateValidRequest());

        Assert.Null(result.Type);
        Assert.Empty(result.Members);
        Assert.Equal("Provider failed.", result.FailureReason);
        Assert.Equal("Retry definition lookup.", result.Guidance);
    }

    private static GetTypeMembersRequest CreateValidRequest() =>
        new(
            SolutionPath: @"C:\repo\Sample.sln",
            FilePath: @"C:\repo\src\File.cs",
            Line: 1,
            Column: 1);

    private sealed class TestTypeMembersQueryDataProvider : ITypeMembersQueryDataProvider
    {
        public GetTypeMembersResult NextResult { get; init; } = new(
            Type: null,
            Members: Array.Empty<TypeMemberDescriptor>(),
            FailureReason: null,
            Guidance: null);

        public ValueTask<GetTypeMembersResult> GetTypeMembersAsync(
            GetTypeMembersRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(NextResult);
        }
    }
}
