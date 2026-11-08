using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Navigation;

/// <summary>Projects overview outcomes without losing failure-stage paths or treating cancellation as an error.</summary>
public sealed class FileOverviewQueryService(FileOverviewQueryOperation operation) : IFileOverviewQueryService
{
    /// <inheritdoc />
    public async ValueTask<GetFileOverviewResult> GetFileOverviewAsync(
        GetFileOverviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new GetFileOverviewResult(
                FilePath: error.FilePath,
                Namespaces: Array.Empty<string>(),
                Types: Array.Empty<FileTypeDescriptor>(),
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new GetFileOverviewResult(
                FilePath: null,
                Namespaces: Array.Empty<string>(),
                Types: Array.Empty<FileTypeDescriptor>(),
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
