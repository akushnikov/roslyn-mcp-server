using System.Text;
using Microsoft.CodeAnalysis;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;

namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Compares actual workspace and disk content for a single document path.
/// </summary>
internal sealed class WorkspaceDocumentComparisonService(IRoslynWorkspaceAccessor workspaceAccessor)
{
    private static readonly TimeSpan[] ReadRetryDelays =
    [
        TimeSpan.FromMilliseconds(25),
        TimeSpan.FromMilliseconds(75),
        TimeSpan.FromMilliseconds(150)
    ];

    /// <summary>
    /// Produces a content comparison for the requested workspace file path.
    /// </summary>
    public async ValueTask<WorkspaceDocumentComparison?> CompareAsync(
        string workspacePath,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();

        var session = await workspaceAccessor.GetSessionAsync(workspacePath, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var normalizedFilePath = NormalizePath(filePath);
        var diskFileExists = File.Exists(normalizedFilePath);
        var diskText = diskFileExists
            ? await ReadFileWithRetryAsync(normalizedFilePath, cancellationToken)
            : null;
        var diskReadSucceeded = !diskFileExists || diskText is not null;

        var documentIds = session.Workspace.CurrentSolution.GetDocumentIdsWithFilePath(normalizedFilePath);
        var primaryDocument = documentIds.Length > 0
            ? session.Workspace.CurrentSolution.GetDocument(documentIds[0])
            : null;

        var workspaceText = primaryDocument is not null
            ? (await primaryDocument.GetTextAsync(cancellationToken)).ToString()
            : null;

        return new WorkspaceDocumentComparison(
            FilePath: normalizedFilePath,
            DocumentExistsInWorkspace: primaryDocument is not null,
            DiskFileExists: diskFileExists,
            DiskReadSucceeded: diskReadSucceeded,
            DocumentKey: primaryDocument?.Id.ToString(),
            WorkspaceText: workspaceText,
            DiskText: diskText,
            WorkspaceTextHash: workspaceText is null ? null : TextHashing.Compute(workspaceText),
            DiskTextHash: diskText is null ? null : TextHashing.Compute(diskText));
    }

    private static async ValueTask<string?> ReadFileWithRetryAsync(string filePath, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= ReadRetryDelays.Length; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                return await reader.ReadToEndAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt == ReadRetryDelays.Length)
                {
                    return null;
                }

                await Task.Delay(ReadRetryDelays[attempt], cancellationToken);
            }
        }

        return null;
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);
}
