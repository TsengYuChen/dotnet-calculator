using VehicleGo.Application.Abstractions;

namespace VehicleGo.UnitTests.Fakes;

public sealed class FakeAllowlistProvider(params string[] emails) : IAllowlistProvider
{
    public List<string> Emails { get; } = [.. emails];

    public IReadOnlyCollection<string> GetAllowedEmails() => Emails;
}
