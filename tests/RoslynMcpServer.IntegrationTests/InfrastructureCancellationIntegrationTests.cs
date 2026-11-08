using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Infrastructure.Services;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Infrastructure.DependencyInjection;

namespace RoslynMcpServer.IntegrationTests;

public sealed class InfrastructureCancellationIntegrationTests
{
    [Fact]
    public async Task ProductionEnvironmentAndProbe_PreCancellation_ThrowsCallerToken()
    {
        await using var provider = CreateProvider();
        var runtime = provider.GetRequiredService<IRuntimeEnvironmentService>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var environmentError = await Assert.ThrowsAsync<OperationCanceledException>(() => runtime.GetEnvironmentAsync(cts.Token).AsTask());
        var probeError = await Assert.ThrowsAsync<OperationCanceledException>(() => runtime.ProbeWorkspaceAsync("unused.sln", cts.Token).AsTask());
        Assert.Equal(cts.Token, environmentError.CancellationToken);
        Assert.Equal(cts.Token, probeError.CancellationToken);
    }

    [Fact]
    public async Task RuntimeProcess_RequestedInFlightCancellation_EscapesActualProcessFallback()
    {
        await using var provider = CreateProvider();
        var runtime = provider.GetRequiredService<IRuntimeEnvironmentService>();
        using var cts = new CancellationTokenSource();
        var directory = Path.Combine(Path.GetTempPath(), "roslyn-cancel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var entered = Path.Combine(directory, "entered");
        var release = Path.Combine(directory, "release");
        var exited = Path.Combine(directory, "exited");
        var (executable, arguments) = CreateControlledProcess(entered, release, exited);
        try
        {
            var pending = RunProcessAsync(runtime, executable, arguments, cts.Token);
            await WaitForFileAsync(entered);
            Assert.False(pending.IsCompleted);
            cts.Cancel();
            var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Equal(cts.Token, error.CancellationToken);
        }
        finally
        {
            File.WriteAllText(release, "release");
            if (File.Exists(entered))
                await WaitForFileAsync(exited);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuntimeProcess_OrdinaryStartOrExitFailure_RemainsNullFallback(bool startFailure)
    {
        await using var provider = CreateProvider();
        var runtime = provider.GetRequiredService<IRuntimeEnvironmentService>();
        var executable = startFailure
            ? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-command")
            : OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var arguments = OperatingSystem.IsWindows() ? "/d /c exit 7" : "-c \"exit 7\"";
        Assert.Null(await RunProcessAsync(runtime, executable, arguments, CancellationToken.None));
    }

    [Fact]
    public async Task RuntimeProcess_Success_PreservesStandardOutput()
    {
        await using var provider = CreateProvider();
        var runtime = provider.GetRequiredService<IRuntimeEnvironmentService>();
        var executable = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var arguments = OperatingSystem.IsWindows() ? "/d /c echo fixture-version" : "-c \"printf fixture-version\"";
        Assert.Equal("fixture-version", (await RunProcessAsync(runtime, executable, arguments, CancellationToken.None))!.Trim());
    }

    [Theory]
    [InlineData("throw-canceled")]
    [InlineData("return-snapshot")]
    [InlineData("throw-fault")]
    public async Task Probe_RequestedInFlightCancellation_DoesNotBecomeLoggedLoadFailure(string completion)
    {
        var logs = new ErrorLogProvider();
        await using var provider = CreateProvider(logs);
        var probe = provider.GetRequiredService<IWorkspaceProbeService>();
        var cache = provider.GetRequiredService<IWorkspaceCache>();
        using var cts = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken receivedToken = default;
        Func<MSBuildWorkspace, string, string, CancellationToken, Task<Solution>> open = async (workspace, _, _, token) =>
        {
            receivedToken = token;
            entered.SetResult();
            await release.Task;
            if (completion == "throw-canceled")
                token.ThrowIfCancellationRequested();
            if (completion == "throw-fault")
                throw new InvalidOperationException("Fault racing cancellation");
            return workspace.CurrentSolution;
        };
        SetProbeOpen(probe, open);
        var pending = probe.ProbeAsync(FixtureWorkspacePath(), cts.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cts.Cancel();
        release.SetResult();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(cts.Token, error.CancellationToken);
        Assert.Equal(cts.Token, receivedToken);
        Assert.Empty(logs.Errors);
        Assert.Empty(await cache.ListAsync());
    }

    [Fact]
    public async Task Probe_OrdinaryOpenFailure_RetainsDiagnosticAndErrorLog()
    {
        var logs = new ErrorLogProvider();
        await using var provider = CreateProvider(logs);
        var probe = provider.GetRequiredService<IWorkspaceProbeService>();
        var original = new InvalidOperationException("Original probe failure");
        SetProbeOpen(probe, (_, _, _, _) => Task.FromException<Solution>(original));
        var path = FixtureWorkspacePath();
        var result = await probe.ProbeAsync(path);
        Assert.False(result.CanBeLoaded);
        Assert.Equal(Path.GetFullPath(path), result.SolutionPath);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("WorkspaceLoadFailed", diagnostic.Code);
        Assert.Equal(original.Message, diagnostic.Message);
        Assert.Equal(Path.GetFullPath(path), diagnostic.Path);
        Assert.Same(original, Assert.Single(logs.Errors));
        Assert.Empty(await provider.GetRequiredService<IWorkspaceCache>().ListAsync());
    }

    // Reflection keeps both deterministic test seams outside the public runtime/probe contracts.
    private static Task<string?> RunProcessAsync(IRuntimeEnvironmentService runtime, string executable, string arguments, CancellationToken token)
    {
        var method = runtime.GetType().GetMethod("TryRunProcessAsync", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (Task<string?>)method.Invoke(null, [executable, arguments, token])!;
    }

    private static void SetProbeOpen(IWorkspaceProbeService probe,
        Func<MSBuildWorkspace, string, string, CancellationToken, Task<Solution>> open)
    {
        var property = probe.GetType().GetProperty("ProbeOpenAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property.SetValue(probe, open);
    }

    private static (string Executable, string Arguments) CreateControlledProcess(string entered, string release, string exited)
    {
        if (OperatingSystem.IsWindows())
        {
            static string Quote(string path) => "'" + path.Replace("'", "''") + "'";
            var script = $"[IO.File]::WriteAllText({Quote(entered)},'ready'); $deadline=[DateTime]::UtcNow.AddSeconds(30); while (-not [IO.File]::Exists({Quote(release)}) -and [DateTime]::UtcNow -lt $deadline) {{ Start-Sleep -Milliseconds 10 }}; [IO.File]::WriteAllText({Quote(exited)},'done')";
            return ("powershell.exe", "-NoLogo -NoProfile -NonInteractive -EncodedCommand " +
                Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script)));
        }

        static string ShellQuote(string path) => "'" + path.Replace("'", "'\\''") + "'";
        var command = $"printf ready > {ShellQuote(entered)}; count=0; while [ ! -f {ShellQuote(release)} ] && [ $count -lt 3000 ]; do sleep 0.01; count=$((count + 1)); done; printf done > {ShellQuote(exited)}";
        return ("/bin/sh", "-c \"" + command.Replace("\"", "\\\"") + "\"");
    }

    private static async Task WaitForFileAsync(string path)
    {
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path));
        watcher.Created += (_, _) => arrived.TrySetResult();
        watcher.EnableRaisingEvents = true;
        if (File.Exists(path))
            return;
        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }

    private static string FixtureWorkspacePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "roslyn-mcp-server.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "tests", "Fixtures", "SingleProjectSample", "SingleProjectSample.sln");
    }

    private static ServiceProvider CreateProvider(ErrorLogProvider? logs = null) =>
        new ServiceCollection().AddLogging(builder =>
        {
            if (logs is not null)
                builder.AddProvider(logs);
        }).AddRoslynMcpServer().BuildServiceProvider();

    private sealed class ErrorLogProvider : ILoggerProvider
    {
        public List<Exception> Errors { get; } = [];
        public ILogger CreateLogger(string categoryName) => new ErrorLogger(Errors);
        public void Dispose() { }

        private sealed class ErrorLogger(List<Exception> errors) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Error && exception is not null)
                    errors.Add(exception);
            }
        }
    }
}
