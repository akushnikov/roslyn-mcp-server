namespace RoslynMcpServer.Abstractions.Workspace.Results;

/// <summary>
/// Represents a stable diagnostic emitted during workspace probing or loading.
/// </summary>
public sealed record WorkspaceOperationDiagnostic(
    WorkspaceDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? Path);
