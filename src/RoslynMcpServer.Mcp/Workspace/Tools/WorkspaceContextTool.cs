using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Abstractions.Workspace.Services;

namespace RoslynMcpServer.Mcp.Workspace.Tools;

/// <summary>
/// Resolves the workspace context required for Roslyn-driven tools.
/// </summary>
/// <remarks>
/// This tool is intended to tell the client or developer whether the server already
/// has enough context to work with the current .NET codebase. It prefers an explicit
/// solution path, then client roots, and later may fall back to server configuration.
/// </remarks>
[McpServerToolType]
internal sealed class WorkspaceContextTool(IWorkspaceContextResolver workspaceContextResolver)
{
    /// <summary>
    /// Resolves the current workspace context from explicit input and negotiated client capabilities.
    /// </summary>
    /// <param name="solutionPath">Optional absolute path to the solution file for this request.</param>
    /// <param name="server">The active MCP server session used to inspect client capabilities and request roots.</param>
    /// <param name="cancellationToken">Cancels the tool invocation.</param>
    /// <returns>
    /// A result that indicates whether the workspace context was resolved and, if not,
    /// explains what additional input the client or user must provide.
    /// </returns>
    [McpServerTool(Name = "workspace_context")]
    [Description("Resolves the current workspace context using an explicit solution path, client roots, or server defaults.")]
    public async Task<GetWorkspaceContextResult> GetWorkspaceContext(
        [Description("Optional absolute path to the .sln file to use for this request.")] string? solutionPath,
        McpServer server,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var clientSupportsRoots = server.ClientCapabilities?.Roots is not null;
        var clientRoots = clientSupportsRoots
            ? await GetClientRootsAsync(server, cancellationToken)
            : Array.Empty<WorkspaceRoot>();

        var result = await workspaceContextResolver.ResolveAsync(
            new ResolveWorkspaceContextRequest(
                ExplicitSolutionPath: solutionPath,
                ConfiguredSolutionPath: null,
                ClientRoots: clientRoots,
                ClientSupportsRoots: clientSupportsRoots),
            cancellationToken);

        return new GetWorkspaceContextResult(
            IsResolved: result.IsResolved,
            ClientSupportsRoots: clientSupportsRoots,
            Context: result.Context,
            FailureReason: result.FailureReason,
            Guidance: result.Guidance);
    }

    private static async Task<IReadOnlyList<WorkspaceRoot>> GetClientRootsAsync(
        McpServer server,
        CancellationToken cancellationToken)
    {
        var result = await server.RequestRootsAsync(new ListRootsRequestParams(), cancellationToken);

        return result.Roots
            .Select(root => new WorkspaceRoot(
                Uri: root.Uri,
                Name: root.Name,
                LocalPath: TryGetLocalPath(root.Uri)))
            .ToArray();
    }

    /// <summary>
    /// Converts a file URI into a local file system path when possible.
    /// </summary>
    private static string? TryGetLocalPath(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri) || !parsedUri.IsFile)
        {
            return null;
        }

        return parsedUri.LocalPath;
    }
}
