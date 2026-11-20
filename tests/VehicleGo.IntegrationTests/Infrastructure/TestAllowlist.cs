using System.Collections.Concurrent;
using VehicleGo.Application.Abstractions;

namespace VehicleGo.IntegrationTests.Infrastructure;

/// <summary>Replaceable <c>ALLOWED_EMAILS</c>: tests add and remove e-mails while the app is running.</summary>
public sealed class TestAllowlist : IAllowlistProvider
{
    private readonly ConcurrentDictionary<string, byte> _emails = new(StringComparer.Ordinal);

    public void Add(string email) => _emails[email] = 0;

    public void Remove(string email) => _emails.TryRemove(email, out _);

    public IReadOnlyCollection<string> GetAllowedEmails() => _emails.Keys.ToArray();
}
