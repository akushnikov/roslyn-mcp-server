using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>Defines the application boundary for retrieving members of a resolved type.</summary>
public interface ITypeMembersQueryService
{
    /// <summary>Returns explicit members of the type at a source position.</summary>
    ValueTask<GetTypeMembersResult> GetTypeMembersAsync(
        GetTypeMembersRequest request,
        CancellationToken cancellationToken = default);
}
