using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Provides transport-neutral type-member query data.</summary>
public interface ITypeMembersQueryDataProvider
{
    /// <summary>Resolves and maps members from the loaded source workspace.</summary>
    ValueTask<GetTypeMembersResult> GetTypeMembersAsync(
        GetTypeMembersRequest request,
        CancellationToken cancellationToken = default);
}
