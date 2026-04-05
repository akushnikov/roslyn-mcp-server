using System.Collections.Concurrent;
using RoslynMcpServer.Abstractions.WorkspaceSync.Models;

namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Tracks file changes that are expected to appear on disk after in-process Roslyn mutations.
/// </summary>
internal sealed class ExpectedExternalChangeStore
{
    private readonly ConcurrentDictionary<string, ExpectedExternalChange> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Stores the expected external changes, replacing older entries for the same file.
    /// </summary>
    public void Store(IEnumerable<ExpectedExternalChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        foreach (var change in changes)
        {
            _entries[NormalizePath(change.FilePath)] = change;
        }
    }

    /// <summary>
    /// Returns the tracked change for the specified file path when present.
    /// </summary>
    public bool TryGet(string filePath, out ExpectedExternalChange? change)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var found = _entries.TryGetValue(NormalizePath(filePath), out var entry);
        change = entry;
        return found;
    }

    /// <summary>
    /// Matches the actual disk hash for the specified file path against the stored expectation.
    /// </summary>
    public ExpectedExternalChangeMatchResult Match(
        string filePath,
        string actualDiskHash,
        string actualWorkspaceHash,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualDiskHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualWorkspaceHash);

        if (!TryGet(filePath, out var change) || change is null)
        {
            return new ExpectedExternalChangeMatchResult(
                HasMatch: false,
                IsEcho: false,
                Change: null);
        }

        if (change.ExpiresAtUtc <= utcNow)
        {
            Remove(filePath);
            return new ExpectedExternalChangeMatchResult(
                HasMatch: false,
                IsEcho: false,
                Change: null);
        }

        if (!string.Equals(change.ExpectedTextHash, actualDiskHash, StringComparison.Ordinal))
        {
            return new ExpectedExternalChangeMatchResult(
                HasMatch: false,
                IsEcho: false,
                Change: change);
        }

        var isEcho = string.Equals(actualWorkspaceHash, actualDiskHash, StringComparison.Ordinal);
        return new ExpectedExternalChangeMatchResult(
            HasMatch: true,
            IsEcho: isEcho,
            Change: change);
    }

    /// <summary>
    /// Removes the tracked change for the specified file path when present.
    /// </summary>
    public bool Remove(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return _entries.TryRemove(NormalizePath(filePath), out _);
    }

    /// <summary>
    /// Removes expired expected changes using the supplied clock value.
    /// </summary>
    public int RemoveExpired(DateTimeOffset utcNow)
    {
        var removed = 0;

        foreach (var pair in _entries)
        {
            if (pair.Value.ExpiresAtUtc > utcNow)
            {
                continue;
            }

            if (_entries.TryRemove(pair.Key, out _))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    /// Returns the current number of tracked expected changes.
    /// </summary>
    public int Count => _entries.Count;

    private static string NormalizePath(string path) => Path.GetFullPath(path);
}
