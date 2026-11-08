using Microsoft.Extensions.DependencyInjection;
using RoslynMcpServer.Abstractions.Server.Services;
using RoslynMcpServer.Application.Server;
using RoslynMcpServer.Infrastructure.DependencyInjection;
using RoslynMcpServer.Infrastructure.Server;

namespace RoslynMcpServer.UnitTests.Server;

public sealed class ServerInfoQueryDataProviderTests
{
    [Fact]
    public async Task GetServerInfoAsync_ReturnsRuntimeMetadataAndInjectedVersion()
    {
        var provider = new ServerInfoQueryDataProvider("application-version");
        var before = DateTimeOffset.UtcNow;

        var result = await provider.GetServerInfoAsync();

        Assert.Equal("roslyn-mcp-server", result.Name);
        Assert.Equal("application-version", result.Version);
        Assert.Equal(Environment.Version.ToString(), result.Framework);
        Assert.Equal(Environment.OSVersion.ToString(), result.Os);
        Assert.InRange(result.UtcNow, before, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, result.UtcNow.Offset);
    }

    [Fact]
    public async Task ProductionRegistration_PreservesApplicationAssemblyVersion()
    {
        using var services = new ServiceCollection().AddRoslynMcpServer().BuildServiceProvider();
        var provider = services.GetRequiredService<IServerInfoQueryDataProvider>();

        var result = await provider.GetServerInfoAsync();

        Assert.IsType<ServerInfoQueryDataProvider>(provider);
        Assert.Same(provider, services.GetRequiredService<IServerInfoQueryDataProvider>());
        Assert.Equal(typeof(ServerInfoQueryService).Assembly.GetName().Version?.ToString() ?? "0.1.0", result.Version);
    }

    [Fact]
    public async Task GetServerInfoAsync_CollectsFreshTimestampForEachCall()
    {
        var provider = new ServerInfoQueryDataProvider("version");
        var firstBefore = DateTimeOffset.UtcNow;
        var first = await provider.GetServerInfoAsync();
        var firstAfter = DateTimeOffset.UtcNow;
        var secondBefore = DateTimeOffset.UtcNow;

        var second = await provider.GetServerInfoAsync();

        Assert.NotSame(first, second);
        Assert.InRange(first.UtcNow, firstBefore, firstAfter);
        Assert.InRange(second.UtcNow, secondBefore, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void GetServerInfoAsync_ThrowsForPreCanceledCallerToken()
    {
        var provider = new ServerInfoQueryDataProvider("version");
        using var source = new CancellationTokenSource();
        source.Cancel();

        var exception = Assert.Throws<OperationCanceledException>(() => provider.GetServerInfoAsync(source.Token));

        Assert.Equal(source.Token, exception.CancellationToken);
    }
}
