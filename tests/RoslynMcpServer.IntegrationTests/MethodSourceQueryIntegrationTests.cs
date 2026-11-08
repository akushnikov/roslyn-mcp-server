using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Infrastructure.DependencyInjection;

namespace RoslynMcpServer.IntegrationTests;

public sealed class MethodSourceQueryIntegrationTests
{
    [Fact]
    public async Task InvocationInA_ReturnsDeclarationTextPathAndLineSpanFromB()
    {
        await using var workspace = await TestWorkspace.CreateAsync();
        var request = workspace.RequestFor("A.cs", "Compute");

        var result = await workspace.Service.GetMethodSourceAsync(request);

        Assert.Null(result.FailureReason);
        Assert.Null(result.Guidance);
        Assert.NotNull(result.Signature);
        Assert.Equal("Compute", result.Signature.Name);
        Assert.Equal("Method", result.Signature.Kind);
        Assert.Equal("int", result.Signature.ReturnType);
        Assert.Equal("value", Assert.Single(result.Signature.Parameters).Name);
        Assert.Equal(workspace.DeclarationPath, result.Signature.Definition?.FilePath);
        Assert.NotNull(result.Method);
        Assert.Equal(workspace.DeclarationPath, result.Method.FilePath);
        var declaration = CSharpSyntaxTree.ParseText(TestWorkspace.DeclarationSource)
            .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(static method => method.Identifier.ValueText == "Compute");
        var expectedSpan = declaration.GetLocation().GetLineSpan();
        Assert.Equal(declaration.ToString(), result.Method.Source);
        Assert.Equal(workspace.DeclarationPath, result.Method.Start.FilePath);
        Assert.Equal(workspace.DeclarationPath, result.Method.End.FilePath);
        Assert.Equal(expectedSpan.StartLinePosition.Line + 1, result.Method.Start.Line);
        Assert.Equal(expectedSpan.StartLinePosition.Character + 1, result.Method.Start.Column);
        Assert.Equal(expectedSpan.EndLinePosition.Line + 1, result.Method.End.Line);
        Assert.Equal(expectedSpan.EndLinePosition.Character + 1, result.Method.End.Column);
        // The old code applied B's span to A's much shorter source text and could not return this result.
        Assert.True(declaration.Span.End > TestWorkspace.InvocationSource.Length);
    }

    [Fact]
    public async Task MetadataCallable_RetainsResolvedSignature_WhenSourceUnavailable()
    {
        await using var workspace = await TestWorkspace.CreateAsync();

        var result = await workspace.Service.GetMethodSourceAsync(workspace.RequestFor("A.cs", "ToString"));

        Assert.Null(result.Method);
        Assert.NotNull(result.Signature);
        Assert.Equal("ToString", result.Signature.Name);
        Assert.Equal("Method", result.Signature.Kind);
        Assert.Null(result.Signature.Definition);
        Assert.Equal("The resolved callable member does not have source syntax available.", result.FailureReason);
        Assert.Equal("Use a callable member declared in a source document of the loaded solution.", result.Guidance);
    }

    [Theory]
    [InlineData("Number", "PropertyGet", "get => 42;")]
    [InlineData("Changed", "EventAdd", "add { }")]
    [InlineData("Local", "LocalFunction", "int Local(int value) => value + 1;")]
    public async Task CallableDeclarations_PreserveAccessorAndLocalFunctionMapping(
        string marker, string expectedKind, string expectedSource)
    {
        await using var workspace = await TestWorkspace.CreateAsync();

        var result = await workspace.Service.GetMethodSourceAsync(workspace.RequestFor("B.cs", marker));

        Assert.Null(result.FailureReason);
        Assert.NotNull(result.Signature);
        Assert.Equal(expectedKind, result.Signature.Kind);
        Assert.NotNull(result.Method);
        Assert.Equal(workspace.DeclarationPath, result.Method.FilePath);
        Assert.Equal(expectedSource, result.Method.Source);
        Assert.Equal(workspace.DeclarationPath, result.Method.Start.FilePath);
        Assert.Equal(workspace.DeclarationPath, result.Method.End.FilePath);
    }

    private sealed class TestWorkspace(string rootDirectory, string projectPath, ServiceProvider provider) : IAsyncDisposable
    {
        public const string InvocationSource = "namespace Sample; public class Caller { public int Run() => Target.Compute(2); public string Text() => (1).ToString(); }";

        public const string DeclarationSource = """
            // This document intentionally differs from the short invocation file.
            // Extra declaration offset makes the former source/span mismatch observable.
            namespace Sample;

            public static class Target
            {
                public static int Compute(int value)
                {
                    return value * 2;
                }

                public static int Number
                {
                    get => 42;
                }

                public static event System.Action Changed
                {
                    add { }
                    remove { }
                }

                public static int Host()
                {
                    int Local(int value) => value + 1;
                    return Local(2);
                }
            }
            """;

        public string DeclarationPath => Path.Combine(rootDirectory, "B.cs");
        public IMethodSourceQueryService Service => provider.GetRequiredService<IMethodSourceQueryService>();

        public GetMethodSourceRequest RequestFor(string fileName, string marker)
        {
            var source = fileName == "A.cs" ? InvocationSource : DeclarationSource;
            var offset = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(offset >= 0);
            var position = SourceText.From(source).Lines.GetLinePosition(offset);
            return new GetMethodSourceRequest(projectPath, Path.Combine(rootDirectory, fileName),
                position.Line + 1, position.Character + 1);
        }

        public static async Task<TestWorkspace> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), "roslyn-mcp-method-source-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            ServiceProvider? provider = null;
            try
            {
                var projectPath = Path.Combine(root, "Sample.csproj");
                await File.WriteAllTextAsync(projectPath,
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");
                await File.WriteAllTextAsync(Path.Combine(root, "A.cs"), InvocationSource);
                await File.WriteAllTextAsync(Path.Combine(root, "B.cs"), DeclarationSource);
                provider = new ServiceCollection().AddLogging().AddRoslynMcpServer().BuildServiceProvider();
                await provider.GetRequiredService<IWorkspaceLoader>()
                    .LoadAsync(new LoadSolutionRequest(projectPath, ForceReload: false));
                return new TestWorkspace(root, projectPath, provider);
            }
            catch
            {
                if (provider is not null)
                {
                    await provider.DisposeAsync();
                }

                Directory.Delete(root, recursive: true);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await provider.DisposeAsync();
            Directory.Delete(rootDirectory, recursive: true);
        }
    }
}
