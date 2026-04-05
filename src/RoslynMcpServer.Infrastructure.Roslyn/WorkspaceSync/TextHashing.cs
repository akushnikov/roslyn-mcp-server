using System.Security.Cryptography;
using System.Text;

namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Provides stable text hashing for workspace synchronization comparisons.
/// </summary>
internal static class TextHashing
{
    /// <summary>
    /// Computes a SHA-256 hash for the supplied text.
    /// </summary>
    public static string Compute(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
