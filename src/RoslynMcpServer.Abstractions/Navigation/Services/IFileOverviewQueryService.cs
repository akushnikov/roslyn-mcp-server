using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Returns a source file's namespaces and type summaries through the query pipeline.</summary>
public interface IFileOverviewQueryService
{
    /// <summary>Retrieves the overview of a C# document in a loaded workspace.</summary>
    /// <param name="request">The loaded solution and source document to inspect.</param>
    /// <param name="cancellationToken">Cancels the query without producing an error outcome.</param>
    /// <returns>The overview or a structured failure retaining the available document path.</returns>
    ValueTask<GetFileOverviewResult> GetFileOverviewAsync(
        GetFileOverviewRequest request,
        CancellationToken cancellationToken = default);
}
