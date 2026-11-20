using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Common;
using VehicleGo.Domain.Users;

namespace VehicleGo.Application.Me;

public sealed record UpdatePreferencesCommand(CalendarPreference Calendar);

/// <summary>Changes the current user's calendar; returns the same data as <c>/api/me</c>.</summary>
public sealed class UpdatePreferencesCommandHandler(IUserRepository users, ICurrentUser currentUser, TimeProvider timeProvider)
    : ICommandHandler<UpdatePreferencesCommand, MeDto>
{
    public async Task<Result<MeDto>> HandleAsync(UpdatePreferencesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!Enum.IsDefined(command.Calendar))
        {
            return Result<MeDto>.Failure(ErrorCode.ValidationFailed, "calendar");
        }

        var user = await users.GetAsync(currentUser.UserId, cancellationToken)
            ?? throw new InvalidOperationException("The signed-in user does not exist.");
        user.ChangeCalendarPreference(command.Calendar, timeProvider.GetUtcNow());
        await users.SaveAsync(user, cancellationToken);
        return Result<MeDto>.Success(new MeDto(currentUser.Email ?? string.Empty, user.CalendarPreference));
    }
}
