using VehicleGo.Application.Auth;
using VehicleGo.UnitTests.Fakes;

namespace VehicleGo.UnitTests.Application;

public class LoginAdmissionTests
{
    private static LoginAdmission Admission(params string[] allowlist) => new(new FakeAllowlistProvider(allowlist));

    [Fact]
    public void 名單上且已驗證_通過() =>
        Assert.True(Admission("alice@example.com", "bob@example.com").Admit("bob@example.com", emailVerified: true));

    [Fact]
    public void Email未驗證_拒絕() =>
        Assert.False(Admission("alice@example.com").Admit("alice@example.com", emailVerified: false));

    [Fact]
    public void 不在名單上_拒絕() =>
        Assert.False(Admission("alice@example.com").Admit("mallory@example.com", emailVerified: true));

    [Theory]
    [InlineData("  Alice@Example.COM ")]
    [InlineData("ALICE@EXAMPLE.COM")]
    public void Email去空白並轉小寫後比對(string email) =>
        Assert.True(Admission("alice@example.com").Admit(email, emailVerified: true));

    [Fact]
    public void 名單本身也去空白並轉小寫() =>
        Assert.True(Admission(" Alice@Example.com  ").Admit("alice@example.com", emailVerified: true));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 沒有Email_拒絕(string? email) =>
        Assert.False(Admission("alice@example.com", "").Admit(email, emailVerified: true));

    [Fact]
    public void 名單是空的_全部拒絕() =>
        Assert.False(Admission().Admit("alice@example.com", emailVerified: true));

    [Fact]
    public void 部分相符不算通過() =>
        Assert.False(Admission("alice@example.com").Admit("alice@example.com.evil.example", emailVerified: true));

    [Fact]
    public void 每次請求的檢查只比對名單()
    {
        var allowlist = new FakeAllowlistProvider("alice@example.com");
        var admission = new LoginAdmission(allowlist);

        Assert.True(admission.IsAllowed(" Alice@example.com"));

        allowlist.Emails.Clear();
        Assert.False(admission.IsAllowed("alice@example.com"));
        Assert.False(admission.IsAllowed(null));
    }
}
