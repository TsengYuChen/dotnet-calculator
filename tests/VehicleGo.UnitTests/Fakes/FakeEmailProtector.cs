using System.Text;
using VehicleGo.Application.Abstractions;

namespace VehicleGo.UnitTests.Fakes;

/// <summary>Deterministic, obviously fake "encryption" and "hash" so tests can see what was passed.</summary>
public sealed class FakeEmailProtector : IEmailProtector
{
    public byte[] Encrypt(string email) => Encoding.UTF8.GetBytes("enc:" + email);

    public byte[] ComputeLookupHash(string email) => Encoding.UTF8.GetBytes("hash:" + email.Trim().ToLowerInvariant());
}
