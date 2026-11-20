using System.ComponentModel.DataAnnotations;
using VehicleGo.Domain.Users;

namespace VehicleGo.Web.Contracts;

/// <summary><c>PATCH /api/me/preferences</c>: <c>{ calendar }</c>.</summary>
public sealed class UpdatePreferencesRequest
{
    [Required]
    public CalendarPreference? Calendar { get; init; }
}
