using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace RoslynMcpServer.ProtocolTests;

public sealed class BaseToolsProtocolTests
{
    [Fact]
    public async Task StdioHost_ListsAndCalls_BaseTools()
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "Roslyn MCP Test",
            Command = "dotnet",
            Arguments =
            [
                "run",
                "--project",
                TestPaths.StdioHostProjectPath
            ],
            ShutdownTimeout = TimeSpan.FromSeconds(10)
        });

        await using var client = await McpClient.CreateAsync(transport);
        var tools = await client.ListToolsAsync();

        AssertToolSet(tools.Select(static tool => tool.Name));

        await AssertSuccessfulCallAsync(client, "server_info", null);
        await AssertSuccessfulCallAsync(client, "workspace_context", new Dictionary<string, object?> { ["solutionPath"] = TestPaths.SingleProjectSolutionPath });
        await AssertSuccessfulCallAsync(client, "load_solution", new Dictionary<string, object?> { ["solutionPath"] = TestPaths.SingleProjectSolutionPath });
        await AssertSuccessfulCallAsync(client, "get_workspace_state", new Dictionary<string, object?> { ["solutionPath"] = TestPaths.SingleProjectSolutionPath });
        await AssertSuccessfulCallAsync(client, "get_project_structure", new Dictionary<string, object?> { ["solutionPath"] = TestPaths.SingleProjectSolutionPath, ["includeDocuments"] = true });
        await AssertSuccessfulCallAsync(client, "diagnose", new Dictionary<string, object?> { ["solutionPath"] = TestPaths.SingleProjectSolutionPath, ["verbose"] = true });
    }

    [Fact]
    public async Task HttpHost_ListsAndCalls_BaseTools()
    {
        var port = GetFreePort();
        await using var server = await HttpServerProcess.StartAsync(TestPaths.HttpHostProjectPath, port);

        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri($"http://127.0.0.1:{port}/mcp"),
            TransportMode = HttpTransportMode.AutoDetect
        });

        await using var client = await McpClient.CreateAsync(transport);
        var tools = await client.ListToolsAsync();

        AssertToolSet(tools.Select(static tool => tool.Name));

        await AssertSuccessfulCallAsync(client, "server_info", null);
        await AssertSuccessfulCallAsync(client, "workspace_context", new Dictionary<string, object?> { ["solutionPath"] = TestPaths.SingleProjectSolutionPath });
        await AssertSuccessfulCallAsync(client, "load_solution", new Dictionary<string, object?> { ["solutionPath"] = TestPaths.SingleProjectSolutionPath });
        await AssertSuccessfulCallAsync(client, "get_workspace_state", new Dictionary<string, object?> { ["solutionPath"] = TestPaths.SingleProjectSolutionPath });
        await AssertSuccessfulCallAsync(client, "get_project_structure", new Dictionary<string, object?> { ["solutionPath"] = TestPaths.SingleProjectSolutionPath, ["includeDocuments"] = true });
        await AssertSuccessfulCallAsync(client, "diagnose", new Dictionary<string, object?> { ["solutionPath"] = TestPaths.SingleProjectSolutionPath, ["verbose"] = true });
    }

    private static async Task AssertSuccessfulCallAsync(McpClient client, string toolName, Dictionary<string, object?>? arguments)
    {
        var result = await client.CallToolAsync(toolName, arguments ?? new Dictionary<string, object?>());
        Assert.False(result.IsError ?? false);
        Assert.NotEmpty(result.Content);
    }

    private static void AssertToolSet(IEnumerable<string> toolNames)
    {
        var names = toolNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("server_info", names);
        Assert.Contains("workspace_context", names);
        Assert.Contains("diagnose", names);
        Assert.Contains("load_solution", names);
        Assert.Contains("get_workspace_state", names);
        Assert.Contains("get_project_structure", names);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static class TestPaths
    {
        public static string RepoRoot { get; } = FindRepoRoot();

        public static string StdioHostProjectPath { get; } =
            Path.Combine(RepoRoot, "src", "RoslynMcpServer.Host.Stdio", "RoslynMcpServer.Host.Stdio.csproj");

        public static string HttpHostProjectPath { get; } =
            Path.Combine(RepoRoot, "src", "RoslynMcpServer.Host.Http", "RoslynMcpServer.Host.Http.csproj");

        public static string SingleProjectSolutionPath { get; } =
            Path.Combine(RepoRoot, "tests", "Fixtures", "SingleProjectSample", "SingleProjectSample.sln");

        private static string FindRepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "roslyn-mcp-server.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the repository root.");
        }
    }

    private sealed class HttpServerProcess(Process process) : IAsyncDisposable
    {
        public static async Task<HttpServerProcess> StartAsync(string projectPath, int port)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet", $"run --project \"{projectPath}\" -- --urls http://127.0.0.1:{port}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (process.HasExited)
                {
                    var stdErr = await process.StandardError.ReadToEndAsync();
                    var stdOut = await process.StandardOutput.ReadToEndAsync();
                    throw new InvalidOperationException($"HTTP host exited early.{Environment.NewLine}stdout:{Environment.NewLine}{stdOut}{Environment.NewLine}stderr:{Environment.NewLine}{stdErr}");
                }

                try
                {
                    var response = await client.GetAsync($"http://127.0.0.1:{port}/health");
                    if (response.IsSuccessStatusCode)
                    {
                        return new HttpServerProcess(process);
                    }
                }
                catch
                {
                }

                await Task.Delay(250);
            }

            throw new TimeoutException("Timed out waiting for the HTTP MCP host to become healthy.");
        }

        public async ValueTask DisposeAsync()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            process.Dispose();
        }
    }
}
