using System.Diagnostics;
using Microsoft.Build.Locator;
using RoslynMcpServer.Abstractions.Infrastructure.Models;
using RoslynMcpServer.Abstractions.Infrastructure.Services;
using RoslynMcpServer.Abstractions.Workspace.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Runtime;

/// <summary>
/// Collects runtime metadata and probes workspaces for diagnostics.
/// </summary>
internal sealed class RuntimeEnvironmentService(IWorkspaceProbeService workspaceProbeService) : IRuntimeEnvironmentService
{
    public async ValueTask<EnvironmentDescriptor> GetEnvironmentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dotnetVersion = await TryRunProcessAsync("dotnet", "--version", cancellationToken);

        string msbuildStatus;
        bool isMsBuildAvailable;

        try
        {
            var instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
            isMsBuildAvailable = instances.Length > 0 || MSBuildLocator.IsRegistered;
            msbuildStatus = MSBuildLocator.IsRegistered
                ? "Registered"
                : instances.Length > 0
                    ? $"Discovered {instances.Length} instance(s)"
                    : "No instances discovered";
        }
        catch (Exception exception)
        {
            isMsBuildAvailable = false;
            msbuildStatus = $"Error: {exception.Message}";
        }

        return new EnvironmentDescriptor(
            DotnetSdkVersion: dotnetVersion?.Trim(),
            IsMsBuildAvailable: isMsBuildAvailable,
            MsBuildLocatorStatus: msbuildStatus);
    }

    public ValueTask<WorkspaceHealthDescriptor> ProbeWorkspaceAsync(
        string solutionPath,
        CancellationToken cancellationToken = default)
        => workspaceProbeService.ProbeAsync(solutionPath, cancellationToken);

    private static async Task<string?> TryRunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
