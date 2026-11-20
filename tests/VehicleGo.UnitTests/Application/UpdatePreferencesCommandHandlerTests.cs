using Microsoft.Extensions.Time.Testing;
using VehicleGo.Application.Me;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Users;
using VehicleGo.UnitTests.Fakes;

namespace VehicleGo.UnitTests.Application;

public class UpdatePreferencesCommandHandlerTests
{
    private readonly FakeTimeProvider _time = new(AppTestHarness.Now);
    private readonly FakeUserRepository _users;
    private readonly FakeCurrentUser _currentUser = new();
    private readonly User _mine;
    private readonly User _other;

    public UpdatePreferencesCommandHandlerTests()
    {
        _users = new FakeUserRepository(_time);
        _mine = User.Create(_currentUser.UserId, "sub-alice", [1], [2], AppTestHarness.Now.AddDays(-10));
        _other = User.Create(AppTestHarness.OtherUserId, "sub-bob", [3], [4], AppTestHarness.Now.AddDays(-10));
        _users.Users.AddRange([_mine, _other]);
    }

    private Task<Result<MeDto>> Update(CalendarPreference calendar) =>
        new UpdatePreferencesCommandHandler(_users, _currentUser, _time)
            .HandleAsync(new UpdatePreferencesCommand(calendar), TestContext.Current.CancellationToken);

    [Fact]
    public async Task 修改自己的曆法並儲存_回傳和api_me相同的資料()
    {
        // CUJ 11
        var result = await Update(CalendarPreference.Gregorian);

        Assert.Equal(new MeDto("alice@example.com", CalendarPreference.Gregorian), result.Value);
        Assert.Equal(CalendarPreference.Gregorian, _mine.CalendarPreference);
        Assert.Equal(AppTestHarness.Now, _mine.UpdatedAt);
        Assert.Equal(1, _users.SaveCount);
        Assert.Equal(CalendarPreference.Roc, _other.CalendarPreference);
    }

    [Fact]
    public async Task 不合法的曆法值_VALIDATION_FAILED()
    {
        var result = await Update((CalendarPreference)99);

        Assert.Equal(ErrorCode.ValidationFailed, result.Error!.Code);
        Assert.Equal("calendar", result.Error.Field);
        Assert.Equal(0, _users.SaveCount);
    }
}
