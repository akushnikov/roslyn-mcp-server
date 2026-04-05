using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using RoslynMcpServer.Abstractions.Workspace.Models;

namespace RoslynMcpServer.Infrastructure.Roslyn.Workspace;

/// <summary>
/// Builds transport-safe workspace snapshots from the current Roslyn solution state.
/// </summary>
internal static class WorkspaceSnapshotFactory
{
    public static LoadedWorkspaceSnapshot CreateSnapshot(
        Solution solution,
        string normalizedPath,
        DateTimeOffset loadedAtUtc)
    {
        var projectStructures = solution.Projects
            .Select(CreateProjectStructure)
            .OrderBy(static project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var workspaceDescriptor = new WorkspaceDescriptor(
            SolutionPath: normalizedPath,
            SolutionName: Path.GetFileNameWithoutExtension(normalizedPath),
            LoadedAtUtc: loadedAtUtc,
            ProjectCount: projectStructures.Length,
            DocumentCount: projectStructures.Sum(static project => project.DocumentCount));

        return new LoadedWorkspaceSnapshot(workspaceDescriptor, projectStructures);
    }

    private static ProjectStructureDescriptor CreateProjectStructure(Project project)
    {
        var documents = project.Documents
            .Select(document => new DocumentDescriptor(
                Name: document.Name,
                FilePath: document.FilePath))
            .OrderBy(static document => document.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ProjectStructureDescriptor(
            Name: project.Name,
            FilePath: project.FilePath,
            TargetFrameworks: GetTargetFrameworks(project),
            DocumentCount: documents.Length,
            ProjectReferences: project.ProjectReferences
                .Select(reference => project.Solution.GetProject(reference.ProjectId)?.Name)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .ToArray(),
            Documents: documents);
    }

    private static IReadOnlyList<string> GetTargetFrameworks(Project project)
    {
        if (string.IsNullOrWhiteSpace(project.FilePath) || !File.Exists(project.FilePath))
        {
            return Array.Empty<string>();
        }

        try
        {
            var document = XDocument.Load(project.FilePath);
            var properties = document.Root?
                .Elements()
                .Where(static element => element.Name.LocalName == "PropertyGroup")
                .Elements()
                .ToArray();

            var targetFrameworks = properties?
                .Where(static property =>
                    property.Name.LocalName is "TargetFramework" or "TargetFrameworks")
                .SelectMany(static property => property.Value
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return targetFrameworks is { Length: > 0 }
                ? targetFrameworks
                : Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
