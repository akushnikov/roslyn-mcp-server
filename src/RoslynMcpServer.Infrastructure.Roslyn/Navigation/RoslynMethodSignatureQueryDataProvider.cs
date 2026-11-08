using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>Provides MethodSignature query data; dormant until its migration owner switches the adapter.</summary>
internal sealed class RoslynMethodSignatureQueryDataProvider : IMethodSignatureQueryDataProvider
{
    /// <inheritdoc />
    public ValueTask<GetMethodSignatureResult> GetMethodSignatureAsync(
        GetMethodSignatureRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("The MethodSignature provider is implemented by its Phase 13 migration plan.");
}
