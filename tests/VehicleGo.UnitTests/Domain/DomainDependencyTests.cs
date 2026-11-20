using VehicleGo.Domain.Vehicles;

namespace VehicleGo.UnitTests.Domain;

/// <summary>D-T1: Domain references no other project or framework package (BCL only).</summary>
public class DomainDependencyTests
{
    [Fact]
    public void Domain只參考BCL()
    {
        var references = typeof(Vehicle).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.NotEmpty(references);
        Assert.All(references, name =>
            Assert.True(name == "netstandard" || name == "mscorlib" || name.StartsWith("System", StringComparison.Ordinal),
                $"VehicleGo.Domain must not reference '{name}'."));
    }
}
