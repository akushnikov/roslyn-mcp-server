namespace RoslynMcpServer.Abstractions.Analysis.Models;

/// <summary>
/// Describes compatibility between two resolved types.
/// </summary>
public sealed record TypeCompatibilityDescriptor(
    string SourceType,
    string TargetType,
    bool IsIdentity,
    bool Exists,
    bool IsImplicit,
    bool IsExplicit,
    bool IsReference,
    bool IsBoxing,
    bool IsNumeric,
    bool IsNullable);
