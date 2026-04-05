using RoslynMcpServer.Abstractions.Infrastructure.Models;
using RoslynMcpServer.Abstractions.Server.Results;
using RoslynMcpServer.Abstractions.Workspace.Results;

namespace RoslynMcpServer.Abstractions.Infrastructure.Results;

/// <summary>
/// Represents the server's current environment and optional workspace diagnostic state.
/// </summary>
public sealed record DiagnoseResult(
    GetServerInfoResult Server,
    EnvironmentDescriptor Environment,
    WorkspaceHealthDescriptor? Workspace,
    IReadOnlyList<WorkspaceOperationDiagnostic> Diagnostics);
