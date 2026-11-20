using VehicleGo.Domain.Vehicles;

namespace VehicleGo.UnitTests.Domain;

public class PlateNormalizerTests
{
    [Theory]
    [InlineData("ABC-1234", "ABC1234")]
    [InlineData("abc1234", "ABC1234")]
    [InlineData("abc-1234", "ABC1234")]
    [InlineData("ＡＢＣ－１２３４", "ABC1234")]      // 全形轉半形（NFKC）
    [InlineData("ａｂｃ　１２３４", "ABC1234")]      // 全形小寫 + 全形空白
    [InlineData(" ab c\t12 34 ", "ABC1234")]
    [InlineData("A-B-C-1-2-3-4", "ABC1234")]
    [InlineData("1234-AB", "1234AB")]
    public void 正規化_NFKC_轉大寫_去空白和連字號(string input, string expected)
    {
        Assert.Equal(expected, PlateNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData(" - － 　")]
    public void 正規化後為空字串(string input)
    {
        Assert.Equal(string.Empty, PlateNormalizer.Normalize(input));
    }

    [Fact]
    public void CUJ7_ABC_1234和abc1234視為同一車牌()
    {
        Assert.Equal(PlateNormalizer.Normalize("ABC-1234"), PlateNormalizer.Normalize("abc1234"));
    }
}
