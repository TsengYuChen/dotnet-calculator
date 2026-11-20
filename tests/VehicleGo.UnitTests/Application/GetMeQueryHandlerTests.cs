using Microsoft.Extensions.Time.Testing;
using VehicleGo.Application.Me;
using VehicleGo.Domain.Users;
using VehicleGo.UnitTests.Fakes;

namespace VehicleGo.UnitTests.Application;

public class GetMeQueryHandlerTests
{
    private readonly FakeTimeProvider _time = new(AppTestHarness.Now);
    private readonly FakeUserRepository _users;
    private readonly FakeCurrentUser _currentUser = new();

    public GetMeQueryHandlerTests()
    {
        _users = new FakeUserRepository(_time);
        var mine = User.Create(_currentUser.UserId, "sub-alice", [1], [2], AppTestHarness.Now);
        mine.ChangeCalendarPreference(CalendarPreference.Gregorian, AppTestHarness.Now);
        _users.Users.Add(mine);
        _users.Users.Add(User.Create(AppTestHarness.OtherUserId, "sub-bob", [3], [4], AppTestHarness.Now));
    }

    [Fact]
    public async Task 回傳cookie裡的Email和自己的曆法設定()
    {
        var result = await new GetMeQueryHandler(_users, _currentUser).HandleAsync(new GetMeQuery(), TestContext.Current.CancellationToken);

        Assert.Equal(new MeDto("alice@example.com", CalendarPreference.Gregorian), result.Value);
    }

    [Fact]
    public async Task 別的使用者讀到自己的設定()
    {
        var bob = new FakeCurrentUser { UserId = AppTestHarness.OtherUserId, Email = "bob@example.com" };

        var result = await new GetMeQueryHandler(_users, bob).HandleAsync(new GetMeQuery(), TestContext.Current.CancellationToken);

        Assert.Equal(new MeDto("bob@example.com", CalendarPreference.Roc), result.Value);
    }
}
