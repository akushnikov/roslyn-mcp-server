using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Infrastructure.DependencyInjection;

namespace RoslynMcpServer.IntegrationTests;

public sealed class TypeMembersQueryIntegrationTests
{
    [Fact]
    public async Task ProductionRegistration_ReturnsActualTypeMembers_FromTemporaryWorkspace()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "roslyn-mcp-type-members-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootDirectory);
        var projectPath = Path.Combine(rootDirectory, "Sample.csproj");
        var sourcePath = Path.Combine(rootDirectory, "Example.cs");

        try
        {
            await File.WriteAllTextAsync(projectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>
                </Project>
                """);
            await File.WriteAllTextAsync(sourcePath,
                """
                namespace Sample;

                public sealed class Example
                {
                    public int Zebra;
                    public int Alpha;
                    public void Run() { }
                }
                """);

            await using var provider = new ServiceCollection()
                .AddLogging()
                .AddRoslynMcpServer()
                .BuildServiceProvider();
            var loader = provider.GetRequiredService<IWorkspaceLoader>();
            await loader.LoadAsync(new LoadSolutionRequest(projectPath, ForceReload: false));
            var service = provider.GetRequiredService<ITypeMembersQueryService>();

            var result = await service.GetTypeMembersAsync(new GetTypeMembersRequest(
                projectPath, sourcePath, Line: 3, Column: 21));

            Assert.Null(result.FailureReason);
            Assert.Null(result.Guidance);
            Assert.Equal("Example", result.Type?.Name);
            Assert.Equal(new[] { "Alpha", "Zebra", "Run" }, result.Members.Select(static member => member.Name));
            Assert.Equal(new[] { "Field", "Field", "Method" }, result.Members.Select(static member => member.Kind));
            Assert.All(result.Members, member =>
            {
                Assert.Equal("Public", member.Accessibility);
                Assert.Equal(sourcePath, member.Definition?.FilePath);
                Assert.False(member.IsStatic);
            });
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ProductionRegistration_ResolvesAllSevenDedicatedServices()
    {
        using var provider = new ServiceCollection().AddLogging().AddRoslynMcpServer().BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ITypeMembersQueryService>());
        Assert.NotNull(provider.GetRequiredService<ITypeOverviewQueryService>());
        Assert.NotNull(provider.GetRequiredService<ISymbolAttributesQueryService>());
        Assert.NotNull(provider.GetRequiredService<IMethodSignatureQueryService>());
        Assert.NotNull(provider.GetRequiredService<IMethodSourceQueryService>());
        Assert.NotNull(provider.GetRequiredService<IFileOverviewQueryService>());
        Assert.NotNull(provider.GetRequiredService<IDocumentOutlineQueryService>());
    }

    [Theory]
    [InlineData("TypeMembers", nameof(ITypeMembersQueryService))]
    [InlineData("TypeOverview", nameof(ITypeOverviewQueryService))]
    [InlineData("SymbolAttributes", nameof(ISymbolAttributesQueryService))]
    [InlineData("MethodSignature", nameof(IMethodSignatureQueryService))]
    [InlineData("MethodSource", nameof(IMethodSourceQueryService))]
    [InlineData("FileOverview", nameof(IFileOverviewQueryService))]
    [InlineData("DocumentOutline", nameof(IDocumentOutlineQueryService))]
    public void FinalAdapterConstructor_BindsDedicatedService(string toolName, string serviceType)
    {
        // Inspect the actual adapter declaration without introducing a transport dependency into this test project.
        var sourcePath = Path.Combine(FindRepoRoot(), "src", "RoslynMcpServer.Mcp", "Navigation", "Tools", toolName + "Tool.cs");
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(sourcePath)).GetRoot();
        var adapter = Assert.Single(root.DescendantNodes().OfType<ClassDeclarationSyntax>());
        Assert.NotNull(adapter.ParameterList);
        var parameter = Assert.Single(adapter.ParameterList.Parameters);

        Assert.Equal(serviceType, parameter.Type?.ToString());
    }

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "roslyn-mcp-server.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
