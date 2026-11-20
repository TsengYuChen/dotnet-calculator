using VehicleGo.Application.Reference;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.UnitTests.Application;

public class GetDisplacementBracketsQueryHandlerTests
{
    [Fact]
    public async Task 回傳Domain的級距清單_含標籤和適用車種()
    {
        var result = await new GetDisplacementBracketsQueryHandler()
            .HandleAsync(new GetDisplacementBracketsQuery(), TestContext.Current.CancellationToken);

        var list = result.Value;
        Assert.Equal(DisplacementBrackets.All.Select(b => b.Code), list.Select(b => b.Code));
        Assert.Equal(16, list.Count);
        var car = list.First(b => b.Code == DisplacementBracket.Car1201To1800);
        Assert.Equal("1201–1800cc", car.Label);
        Assert.Equal([VehicleType.Car], car.VehicleTypes);
        Assert.Equal([VehicleType.HeavyMc], list.First(b => b.Code == DisplacementBracket.McGt550).VehicleTypes);
    }
}
