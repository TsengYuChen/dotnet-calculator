using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Users;

namespace VehicleGo.Application.Me;

/// <summary><c>/api/me</c> response: the cookie's e-mail and the stored calendar preference.</summary>
public sealed record MeDto(string Email, CalendarPreference Calendar);

public sealed record GetMeQuery;

/// <summary>Reads through <see cref="IUserRepository"/> (the D-T2 exception).</summary>
public sealed class GetMeQueryHandler(IUserRepository users, ICurrentUser currentUser)
    : IQueryHandler<GetMeQuery, MeDto>
{
    public async Task<Result<MeDto>> HandleAsync(GetMeQuery query, CancellationToken cancellationToken)
    {
        var calendar = await users.GetCalendarPreferenceAsync(currentUser.UserId, cancellationToken);
        return Result<MeDto>.Success(new MeDto(currentUser.Email ?? string.Empty, calendar));
    }
}
