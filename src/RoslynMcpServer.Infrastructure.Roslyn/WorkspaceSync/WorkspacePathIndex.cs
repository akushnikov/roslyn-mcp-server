using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;

namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Maintains path-based indexes used by synchronization logic to map files back to workspace entities.
/// </summary>
internal sealed class WorkspacePathIndex
{
    private readonly ConcurrentDictionary<string, string> _documentKeysByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _pathsByDocumentKey = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Associates the specified file path with a transport-safe document key.
    /// </summary>
    public void SetDocumentKey(string filePath, string documentKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentKey);

        var normalizedPath = NormalizePath(filePath);
        _documentKeysByPath[normalizedPath] = documentKey;
        _pathsByDocumentKey[documentKey] = normalizedPath;
    }

    /// <summary>
    /// Returns the document key for the specified file path when present.
    /// </summary>
    public bool TryGetDocumentKey(string filePath, out string? documentKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var found = _documentKeysByPath.TryGetValue(NormalizePath(filePath), out var key);
        documentKey = key;
        return found;
    }

    /// <summary>
    /// Clears the current index contents.
    /// </summary>
    public void Clear() => _documentKeysByPath.Clear();

    /// <summary>
    /// Rebuilds the index from the supplied Roslyn solution.
    /// </summary>
    public void Rebuild(Solution solution)
    {
        ArgumentNullException.ThrowIfNull(solution);

        _documentKeysByPath.Clear();
        _pathsByDocumentKey.Clear();

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (string.IsNullOrWhiteSpace(document.FilePath))
                {
                    continue;
                }

                SetDocumentKey(document.FilePath, document.Id.ToString());
            }
        }
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);
}
