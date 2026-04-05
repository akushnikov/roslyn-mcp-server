using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Mcp.Navigation.Tools;

/// <summary>
/// Searches source declarations across a previously loaded solution.
/// </summary>
[McpServerToolType]
internal sealed class SearchSymbolsTool(INavigationService navigationService)
{
    /// <summary>
    /// Searches source declarations by name and optional symbol kind.
    /// </summary>
    [McpServerTool(Name = "search_symbols")]
    [Description("Searches source declarations across a previously loaded solution.")]
    public ValueTask<SearchSymbolsResult> SearchSymbols(
        [Description("Absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string solutionPath,
        [Description("Case-insensitive substring to match against source symbol names.")] string query,
        [Description("Optional symbol kind filter such as Class, Interface, Method, Property, Field, Event, or Constant.")] string? kindFilter = null,
        [Description("Maximum number of symbols to return.")] int maxResults = 50,
        CancellationToken cancellationToken = default)
        => navigationService.SearchSymbolsAsync(
            new SearchSymbolsRequest(solutionPath, query, kindFilter, maxResults),
            cancellationToken);
}
