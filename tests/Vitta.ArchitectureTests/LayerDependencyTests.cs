using System.Reflection;
using NetArchTest.Rules;

namespace Vitta.ArchitectureTests;

/// <summary>Garante a regra de dependencia da Clean Architecture: as setas apontam para dentro.</summary>
public class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(Domain.Common.Entity).Assembly;
    private static readonly Assembly Application = typeof(Application.AssemblyReference).Assembly;
    private static readonly Assembly Infrastructure = typeof(Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly Persistence = typeof(Persistence.Context.VittaDbContext).Assembly;

    [Fact]
    public void Domain_DependsOnNothingElse() =>
        AssertNoDependency(Domain, "Vitta.Application", "Vitta.Infrastructure", "Vitta.Persistence", "Vitta.API", "Microsoft.EntityFrameworkCore", "Mediator");

    [Fact]
    public void Application_DoesNotDependOnOuterLayers() =>
        AssertNoDependency(Application, "Vitta.Infrastructure", "Vitta.Persistence", "Vitta.API", "Microsoft.EntityFrameworkCore");

    [Fact]
    public void Infrastructure_DoesNotDependOnPersistenceOrApi() =>
        AssertNoDependency(Infrastructure, "Vitta.Persistence", "Vitta.API");

    [Fact]
    public void Persistence_DoesNotDependOnInfrastructureOrApi() =>
        AssertNoDependency(Persistence, "Vitta.Infrastructure", "Vitta.API");

    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        Assert.True(result.IsSuccessful, "Dependencias proibidas em: " + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
