using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Application.Analysis;
using RoslynMcpServer.Application.Analysis.Operations;

namespace RoslynMcpServer.UnitTests.Analysis;

public sealed class TypeHierarchyQueryServiceTests
{
    [Fact]
    public async Task GetTypeHierarchyAsync_PassesSuccessfulPayloadThroughUnchanged()
    {
        var expected = CreatePayload();
        var provider = new TestProvider { NextResult = expected };

        var result = await CreateService(provider).GetTypeHierarchyAsync(CreateRequest());

        Assert.Same(expected, result);
        Assert.Same(expected.Symbol, result.Symbol);
        Assert.Same(expected.BaseTypes, result.BaseTypes);
        Assert.Same(expected.DerivedTypes, result.DerivedTypes);
        Assert.Same(expected.Interfaces, result.Interfaces);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task GetTypeHierarchyAsync_MapsFailure_PreservingSymbolAndDroppingAllHierarchyCollections()
    {
        var expected = CreatePayload() with { FailureReason = "Lookup failed.", Guidance = "Retry hierarchy." };
        var provider = new TestProvider { NextResult = expected };

        var result = await CreateService(provider).GetTypeHierarchyAsync(CreateRequest());

        Assert.Same(expected.Symbol, result.Symbol);
        AssertEmptyHierarchy(result);
        Assert.Equal(expected.FailureReason, result.FailureReason);
        Assert.Equal(expected.Guidance, result.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task GetTypeHierarchyAsync_ProjectsValidationFailure_WithoutCallingProvider()
    {
        var provider = new TestProvider();

        var result = await CreateService(provider).GetTypeHierarchyAsync(CreateRequest() with { Line = 0 });

        Assert.Null(result.Symbol);
        AssertEmptyHierarchy(result);
        Assert.Equal("Line and column must be positive 1-based values.", result.FailureReason);
        Assert.Equal("Use 1-based line and column values within the target source file.", result.Guidance);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task GetTypeHierarchyAsync_ProjectsSafeFault_WithoutPartialContext()
    {
        var provider = new TestProvider { NextException = new InvalidOperationException("sensitive backend details") };

        var result = await CreateService(provider).GetTypeHierarchyAsync(CreateRequest());

        Assert.Null(result.Symbol);
        AssertEmptyHierarchy(result);
        Assert.Equal("The server could not compute the type hierarchy for the resolved symbol.", result.FailureReason);
        Assert.Equal("Retry the request on a type declaration or a member that belongs to a named type.", result.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetTypeHierarchyAsync_ProjectsCancellation_WithStablePublicPayload(bool cancelInFlight)
    {
        using var source = new CancellationTokenSource();
        if (!cancelInFlight)
        {
            await source.CancelAsync();
        }
        var provider = new TestProvider { CancelOnCall = cancelInFlight ? source : null };

        var result = await CreateService(provider).GetTypeHierarchyAsync(CreateRequest(), source.Token);

        Assert.Null(result.Symbol);
        AssertEmptyHierarchy(result);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
        Assert.Equal(cancelInFlight ? 1 : 0, provider.Calls);
    }

    private static void AssertEmptyHierarchy(GetTypeHierarchyResult result)
    {
        Assert.Empty(result.BaseTypes);
        Assert.Empty(result.DerivedTypes);
        Assert.Empty(result.Interfaces);
        Assert.IsAssignableFrom<IReadOnlyList<SymbolDescriptor>>(result.BaseTypes);
        Assert.IsAssignableFrom<IReadOnlyList<SymbolDescriptor>>(result.DerivedTypes);
        Assert.IsAssignableFrom<IReadOnlyList<SymbolDescriptor>>(result.Interfaces);
    }

    private static TypeHierarchyQueryService CreateService(TestProvider provider) =>
        new(new TypeHierarchyQueryOperation(provider, NullLogger<TypeHierarchyQueryOperation>.Instance));

    private static GetTypeHierarchyRequest CreateRequest() => new("Sample.sln", "File.cs", 1, 1, null);

    private static SymbolDescriptor CreateSymbol(string name) => new(name, "Class", $"Sample.{name}", "Sample", new("File.cs", 1, 1));

    private static GetTypeHierarchyResult CreatePayload() => new(CreateSymbol("Target"),
        [CreateSymbol("Base")], [CreateSymbol("Derived")], [CreateSymbol("IContract")], null, null);

    private sealed class TestProvider : ITypeHierarchyQueryDataProvider
    {
        public int Calls { get; private set; }
        public GetTypeHierarchyResult NextResult { get; init; } = CreatePayload();
        public Exception? NextException { get; init; }
        public CancellationTokenSource? CancelOnCall { get; init; }

        public async ValueTask<GetTypeHierarchyResult> GetTypeHierarchyAsync(GetTypeHierarchyRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (CancelOnCall is not null)
            {
                await CancelOnCall.CancelAsync();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return NextException is not null ? throw NextException : NextResult;
        }
    }
}
