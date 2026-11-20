using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using VehicleGo.Domain.Vehicles;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace VehicleGo.UnitTests.Architecture;

/// <summary>
/// D-T1 dependency direction. Rules are written by namespace, so they also apply once the
/// Infrastructure and Web projects exist (add their assemblies to <see cref="Layers"/> then).
/// </summary>
public class LayerDependencyTests
{
    private static readonly ArchUnitNET.Domain.Architecture Layers = new ArchLoader()
        .LoadAssemblies(typeof(Vehicle).Assembly, typeof(VehicleGo.Application.DependencyInjection).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInNamespaceMatching(@"^VehicleGo\.Domain(\..+)?$").As("Domain layer");

    private static readonly IObjectProvider<IType> ApplicationLayer =
        Types().That().ResideInNamespaceMatching(@"^VehicleGo\.Application(\..+)?$").As("Application layer");

    /// <summary>
    /// Matches dependency targets by namespace (also types of assemblies that are not loaded, such as
    /// EF Core or the future Infrastructure / Web projects).
    /// </summary>
    private static string NamespacePattern(string root) =>
        $"^{root.Replace(".", @"\.", StringComparison.Ordinal)}(\\..+)?$";

    private static IArchRule MustNotDependOn(IObjectProvider<IType> layer, string forbiddenRoot) =>
        Types().That().Are(layer).Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(NamespacePattern(forbiddenRoot));

    [Fact]
    public void 兩個專案的型別都已載入()
    {
        Assert.NotEmpty(DomainLayer.GetObjects(Layers));
        Assert.NotEmpty(ApplicationLayer.GetObjects(Layers));
    }

    [Theory]
    [InlineData("VehicleGo.Application")]
    [InlineData("VehicleGo.Infrastructure")]
    [InlineData("VehicleGo.Web")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.Extensions")]
    [InlineData("Npgsql")]
    public void Domain不依賴其他層和框架(string forbidden) =>
        MustNotDependOn(DomainLayer, forbidden).Check(Layers);

    [Theory]
    [InlineData("VehicleGo.Infrastructure")]
    [InlineData("VehicleGo.Web")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Npgsql")]
    public void Application不依賴Infrastructure_Web_EFCore_ASPNET(string forbidden) =>
        MustNotDependOn(ApplicationLayer, forbidden).Check(Layers);

    [Fact]
    public void 規則能偵測到外部套件的依賴()
    {
        // Sanity check: Application does use Microsoft.Extensions.Logging (allowed), so this rule must fail.
        var rule = MustNotDependOn(ApplicationLayer, "Microsoft.Extensions.Logging");

        Assert.False(rule.HasNoViolations(Layers));
    }

    [Fact]
    public void Application只參考Domain和兩個抽象套件()
    {
        var references = typeof(VehicleGo.Application.DependencyInjection).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.Contains("VehicleGo.Domain", references);
        Assert.All(references, name => Assert.True(
            name is "netstandard" or "VehicleGo.Domain"
                or "Microsoft.Extensions.DependencyInjection.Abstractions"
                or "Microsoft.Extensions.Logging.Abstractions"
            || name.StartsWith("System", StringComparison.Ordinal),
            $"VehicleGo.Application must not reference '{name}'."));
    }
}
