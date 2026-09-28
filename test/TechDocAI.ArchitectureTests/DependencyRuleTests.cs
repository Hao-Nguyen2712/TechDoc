using NetArchTest.Rules;
using TechDocAI.Application.Abstractions;
using TechDocAI.Core.Entities;
using TechDocAI.Infrastructure.Persistence;

namespace TechDocAI.ArchitectureTests;

// Dependency rules (four-project layering, ports-follow-consumer).
// The composition-root exception applies to Program.cs only.
public class DependencyRuleTests
{
    [Fact]
    public void Core_DoesNotDependOn_OuterLayers()
    {
        var result = Types.InAssembly(typeof(Document).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("TechDocAI.Application", "TechDocAI.Infrastructure", "TechDocAI.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_DoesNotDependOn_Infrastructure_Or_Api()
    {
        var result = Types.InAssembly(typeof(IDocumentRepository).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("TechDocAI.Infrastructure", "TechDocAI.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_DoesNotDependOn_Api()
    {
        var result = Types.InAssembly(typeof(TechDocDbContext).Assembly)
            .ShouldNot()
            .HaveDependencyOn("TechDocAI.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Api_OnlyCompositionRootDependsOn_Infrastructure()
    {
        var result = Types.InAssembly(typeof(global::Program).Assembly)
            .That()
            .DoNotHaveNameMatching("Program")
            .ShouldNot()
            .HaveDependencyOn("TechDocAI.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Core_And_Application_DoNotUse_EfCore()
    {
        foreach (var assembly in new[] { typeof(Document).Assembly, typeof(IDocumentRepository).Assembly })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("Microsoft.EntityFrameworkCore")
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(result));
        }
    }

    [Fact]
    public void Core_And_Application_DoNotUse_RawHttpOrFileSystemIo()
    {
        // Stream is allowed outside Infrastructure: it is the shape of the
        // IDocumentStorage port. Raw file-system APIs and HTTP are not.
        foreach (var assembly in new[] { typeof(Document).Assembly, typeof(IDocumentRepository).Assembly })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(
                    "System.Net.Http",
                    "System.IO.File",
                    "System.IO.Directory",
                    "System.IO.StreamReader",
                    "System.IO.StreamWriter",
                    "System.IO.FileStream")
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(result));
        }
    }

    private static string Describe(TestResult result) =>
        "Violating types: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>());
}
