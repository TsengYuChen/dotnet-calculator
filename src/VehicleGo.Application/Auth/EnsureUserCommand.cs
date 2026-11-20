using VehicleGo.Application.Abstractions;
using VehicleGo.Domain.Common;

namespace VehicleGo.Application.Auth;

/// <summary>Creates or updates the user on sign-in (after <see cref="LoginAdmission"/> passed).</summary>
public sealed record EnsureUserCommand(string GoogleSub, string Email);

/// <summary>Returns <c>users.id</c>. Concurrent first logins create one row (upsert in the repository).</summary>
public sealed class EnsureUserCommandHandler(IUserRepository users, IEmailProtector emailProtector)
    : ICommandHandler<EnsureUserCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(EnsureUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.GoogleSub);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Email);

        var id = await users.UpsertOnLoginAsync(
            command.GoogleSub,
            emailProtector.Encrypt(command.Email),
            emailProtector.ComputeLookupHash(command.Email),
            cancellationToken);
        return Result.Success(id);
    }
}
