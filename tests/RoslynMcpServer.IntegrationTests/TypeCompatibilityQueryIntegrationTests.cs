using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Infrastructure.DependencyInjection;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;

namespace RoslynMcpServer.IntegrationTests;

public sealed class TypeCompatibilityQueryIntegrationTests
{
    [Theory]
    [InlineData("Integer", "Integer")]
    [InlineData("Integer", "Wide")]
    [InlineData("Wide", "Integer")]
    [InlineData("Child", "Parent")]
    [InlineData("Parent", "Child")]
    [InlineData("Child", "Unrelated")]
    [InlineData("Integer", "Object")]
    [InlineData("Object", "Integer")]
    [InlineData("Integer", "NullableInteger")]
    [InlineData("NullableInteger", "Integer")]
    [InlineData("Convertible", "Integer")]
    [InlineData("Integer", "Convertible")]
    public async Task RealProvider_ReportsActualDirectionalRoslynConversionFlags(string sourceField, string targetField)
    {
        using var fixture = new TestWorkspace();
        var result = await fixture.Service.CheckTypeCompatibilityAsync(fixture.Request("Types.cs", sourceField, "Types.cs", targetField));

        Assert.Null(result.FailureReason);
        Assert.NotNull(result.Source);
        Assert.NotNull(result.Target);
        var compilation = (CSharpCompilation)(await fixture.TypesProject.GetCompilationAsync())!;
        var fields = compilation.GetTypeByMetadataName("Sample.Fields")!;
        var sourceType = ((IFieldSymbol)fields.GetMembers(sourceField).Single()).Type;
        var targetType = ((IFieldSymbol)fields.GetMembers(targetField).Single()).Type;
        var conversion = compilation.ClassifyConversion(sourceType, targetType);
        AssertDescriptor(result.Compatibility, sourceType, targetType, conversion);
    }

    [Fact]
    public async Task Regression_NumericWideningAndNarrowingAreNotBothImplicit()
    {
        using var fixture = new TestWorkspace();
        var widening = await fixture.Service.CheckTypeCompatibilityAsync(fixture.Request("Types.cs", "Integer", "Types.cs", "Wide"));
        var narrowing = await fixture.Service.CheckTypeCompatibilityAsync(fixture.Request("Types.cs", "Wide", "Types.cs", "Integer"));

        Assert.NotNull(widening.Compatibility);
        Assert.NotNull(narrowing.Compatibility);
        Assert.True(widening.Compatibility.IsImplicit);
        Assert.False(widening.Compatibility.IsExplicit);
        Assert.True(narrowing.Compatibility.IsExplicit);
        Assert.False(narrowing.Compatibility.IsImplicit);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReferencedProjectTypes_AreReboundIntoTheDependentCompilation(bool reverse)
    {
        using var fixture = new TestWorkspace();
        var request = reverse
            ? fixture.Request("Base.cs", "BaseType", "Types.cs", "DerivedType")
            : fixture.Request("Types.cs", "DerivedType", "Base.cs", "BaseType");

        var result = await fixture.Service.CheckTypeCompatibilityAsync(request);

        Assert.Null(result.FailureReason);
        Assert.NotNull(result.Source);
        Assert.NotNull(result.Target);
        var compilation = (CSharpCompilation)(await fixture.TypesProject.GetCompilationAsync())!;
        var derived = compilation.GetTypeByMetadataName("Sample.DerivedType")!;
        var parent = compilation.GetTypeByMetadataName("Library.BaseType")!;
        var source = reverse ? parent : derived;
        var target = reverse ? derived : parent;
        AssertDescriptor(result.Compatibility, source, target, compilation.ClassifyConversion(source, target));
        Assert.Equal(!reverse, result.Compatibility!.IsImplicit);
        Assert.Equal(reverse, result.Compatibility.IsExplicit);
    }

    [Fact]
    public async Task UnreferencedProjects_FailSafelyWithBothResolvedDescriptors()
    {
        using var fixture = new TestWorkspace();
        var result = await fixture.Service.CheckTypeCompatibilityAsync(fixture.Request("Types.cs", "DerivedType", "Isolated.cs", "IsolatedType"));

        Assert.Equal("DerivedType", result.Source?.Name);
        Assert.Equal("IsolatedType", result.Target?.Name);
        Assert.Null(result.Compatibility);
        Assert.Equal("The requested types could not be resolved in a shared C# compilation context.", result.FailureReason);
        Assert.NotNull(result.Guidance);
    }

    [Fact]
    public async Task MissingTarget_PreservesSourceOnly()
    {
        using var fixture = new TestWorkspace();
        var request = fixture.Request("Types.cs", "DerivedType", "Base.cs", "BaseType") with
        {
            TargetFilePath = fixture.PathFor("Missing.cs")
        };

        var result = await fixture.Service.CheckTypeCompatibilityAsync(request);

        Assert.Equal("DerivedType", result.Source?.Name);
        Assert.Null(result.Target);
        Assert.Null(result.Compatibility);
        Assert.Equal("The requested file was not found in the loaded solution.", result.FailureReason);
    }

    [Fact]
    public async Task TypeExtractionFailure_PreservesAvailableSymbolDescriptors()
    {
        using var fixture = new TestWorkspace();
        var result = await fixture.Service.CheckTypeCompatibilityAsync(fixture.Request("Types.cs", "DerivedType", "Namespace.cs", "UnsupportedNamespace"));

        Assert.Equal("DerivedType", result.Source?.Name);
        // Namespace symbols intentionally have no public SymbolDescriptor mapping.
        Assert.Null(result.Target);
        Assert.Null(result.Compatibility);
        Assert.Equal("The requested symbols do not both resolve to analyzable types.", result.FailureReason);
        Assert.Equal("Use type declarations, typed members, parameters, locals, or fields.", result.Guidance);
    }

    [Fact]
    public async Task ProviderPreCancellation_PropagatesAndServiceProjectsStableResult()
    {
        using var fixture = new TestWorkspace();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var request = fixture.Request("Types.cs", "Integer", "Types.cs", "Wide");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Provider
            .GetRequiredService<ITypeCompatibilityQueryDataProvider>()
            .CheckTypeCompatibilityAsync(request, cancellation.Token).AsTask());
        var result = await fixture.Service.CheckTypeCompatibilityAsync(request, cancellation.Token);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Null(result.Source);
        Assert.Null(result.Target);
        Assert.Null(result.Compatibility);
    }

    private static void AssertDescriptor(TypeCompatibilityDescriptor? actual, ITypeSymbol source, ITypeSymbol target, Conversion conversion)
    {
        Assert.NotNull(actual);
        Assert.Equal(source.ToDisplayString(), actual.SourceType);
        Assert.Equal(target.ToDisplayString(), actual.TargetType);
        Assert.Equal(conversion.IsIdentity, actual.IsIdentity);
        Assert.Equal(conversion.Exists, actual.Exists);
        Assert.Equal(conversion.IsImplicit, actual.IsImplicit);
        Assert.Equal(conversion.IsExplicit, actual.IsExplicit);
        Assert.Equal(conversion.IsReference, actual.IsReference);
        Assert.Equal(conversion.IsBoxing, actual.IsBoxing);
        Assert.Equal(conversion.IsNumeric, actual.IsNumeric);
        Assert.Equal(conversion.IsNullable, actual.IsNullable);
    }

    private sealed class TestWorkspace : IDisposable
    {
        private const string Types = """
            namespace Sample;
            public class ParentType { }
            public class ChildType : ParentType { }
            public sealed class UnrelatedType { }
            public class DerivedType : Library.BaseType { }
            public struct ConvertibleType
            {
                public static implicit operator int(ConvertibleType value) => 0;
                public static explicit operator ConvertibleType(int value) => default;
            }
            public class Fields
            {
                public int Integer;
                public long Wide;
                public ChildType Child;
                public ParentType Parent;
                public UnrelatedType Unrelated;
                public object Object;
                public int? NullableInteger;
                public ConvertibleType Convertible;
            }
            """;
        private static readonly IReadOnlyDictionary<string, string> Sources = new Dictionary<string, string>
        {
            ["Types.cs"] = Types,
            ["Base.cs"] = "namespace Library; public class BaseType { }",
            ["Isolated.cs"] = "namespace Isolated; public class IsolatedType { }",
            ["Namespace.cs"] = "namespace UnsupportedNamespace { public class Nested { } }"
        };
        private readonly AdhocWorkspace _workspace = new();
        private readonly string _root = Path.Combine(Path.GetTempPath(), "compatibility-query-tests", Guid.NewGuid().ToString("N"));
        public Project TypesProject { get; }
        public ServiceProvider Provider { get; }
        public ITypeCompatibilityQueryService Service => Provider.GetRequiredService<ITypeCompatibilityQueryService>();

        public TestWorkspace()
        {
            var library = _workspace.AddProject("Library", LanguageNames.CSharp);
            var app = _workspace.AddProject("App", LanguageNames.CSharp);
            var isolated = _workspace.AddProject("Isolated", LanguageNames.CSharp);
            var solution = _workspace.CurrentSolution;
            foreach (var project in new[] { library, app, isolated })
            {
                solution = solution.WithProjectCompilationOptions(project.Id, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .AddMetadataReference(project.Id, MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            }
            solution = solution.AddProjectReference(app.Id, new ProjectReference(library.Id));
            foreach (var (name, source) in Sources)
            {
                var projectId = name == "Base.cs" ? library.Id : name == "Isolated.cs" ? isolated.Id : app.Id;
                solution = solution.AddDocument(DocumentId.CreateNewId(projectId), name, SourceText.From(source), filePath: PathFor(name));
            }
            TypesProject = solution.GetProject(app.Id)!;
            Provider = new ServiceCollection().AddLogging().AddRoslynMcpServer()
                .AddSingleton<IWorkspaceSessionProvider>(new SessionProvider(new RoslynWorkspaceSession(null!, _workspace, solution)))
                .BuildServiceProvider();
        }

        public string PathFor(string file) => Path.Combine(_root, file);

        public CheckTypeCompatibilityRequest Request(string sourceFile, string sourceMarker, string targetFile, string targetMarker)
        {
            var source = Position(sourceFile, sourceMarker);
            var target = Position(targetFile, targetMarker);
            return new(PathFor("Compatibility.sln"), PathFor(sourceFile), source.Line + 1, source.Character + 1,
                PathFor(targetFile), target.Line + 1, target.Character + 1);
        }

        private static LinePosition Position(string file, string marker)
        {
            var source = Sources[file];
            var offset = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(offset >= 0, $"Marker {marker} must exist in {file}.");
            return SourceText.From(source).Lines.GetLinePosition(offset);
        }

        public void Dispose()
        {
            Provider.Dispose();
            _workspace.Dispose();
        }
    }

    private sealed class SessionProvider(RoslynWorkspaceSession session) : IWorkspaceSessionProvider
    {
        public ValueTask<RoslynWorkspaceSession?> GetReadySessionAsync(string solutionPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<RoslynWorkspaceSession?>(session);
        }

        public ValueTask<LoadedWorkspaceSnapshot?> GetReadySnapshotAsync(string solutionPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<LoadedWorkspaceSnapshot?>(null);
        }
    }
}
