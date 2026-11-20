using System.Text;
using Microsoft.Extensions.Time.Testing;
using VehicleGo.Application.Auth;
using VehicleGo.Domain.Users;
using VehicleGo.UnitTests.Fakes;

namespace VehicleGo.UnitTests.Application;

public class EnsureUserCommandHandlerTests
{
    private readonly FakeUserRepository _users = new(new FakeTimeProvider(AppTestHarness.Now));
    private EnsureUserCommandHandler Handler => new(_users, new FakeEmailProtector());

    [Fact]
    public async Task 第一次登入_建立使用者並存加密Email與查詢雜湊()
    {
        var result = await Handler.HandleAsync(new EnsureUserCommand("google-sub-1", "alice@example.com"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var user = Assert.Single(_users.Users);
        Assert.Equal(user.Id, result.Value);
        Assert.Equal("google-sub-1", user.GoogleSub);
        Assert.Equal("enc:alice@example.com", Encoding.UTF8.GetString(user.EmailEncrypted));
        Assert.Equal("hash:alice@example.com", Encoding.UTF8.GetString(user.EmailLookupHash));
        Assert.Equal(CalendarPreference.Roc, user.CalendarPreference);
    }

    [Fact]
    public async Task 再次登入_回傳同一個使用者ID且不重複建立()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = await Handler.HandleAsync(new EnsureUserCommand("google-sub-1", "alice@example.com"), ct);
        var second = await Handler.HandleAsync(new EnsureUserCommand("google-sub-1", "alice.new@example.com"), ct);

        Assert.Equal(first.Value, second.Value);
        var user = Assert.Single(_users.Users);
        Assert.Equal("enc:alice.new@example.com", Encoding.UTF8.GetString(user.EmailEncrypted));
    }
}
