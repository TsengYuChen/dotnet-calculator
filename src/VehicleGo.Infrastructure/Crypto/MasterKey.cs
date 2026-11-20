using System.Diagnostics.CodeAnalysis;

namespace VehicleGo.Infrastructure.Crypto;

/// <summary>
/// <c>MASTER_KEY</c>: Base64 of exactly 32 bytes (design §7). Every encryption key is derived from it with
/// <see cref="KeyDerivation"/>; the master key itself is never used directly and never logged.
/// </summary>
public sealed class MasterKey
{
    public const string ConfigurationKey = "MASTER_KEY";
    public const int SizeInBytes = 32;

    private const string InvalidMessage = "MASTER_KEY must be the Base64 encoding of exactly 32 bytes (openssl rand -base64 32).";

    private readonly byte[] _key;

    private MasterKey(byte[] key) => _key = key;

    public static MasterKey FromBytes(ReadOnlySpan<byte> key)
    {
        if (key.Length != SizeInBytes)
        {
            throw new ArgumentException(InvalidMessage, nameof(key));
        }

        return new MasterKey(key.ToArray());
    }

    public static bool TryParse(string? base64, [NotNullWhen(true)] out MasterKey? masterKey)
    {
        masterKey = null;
        if (string.IsNullOrWhiteSpace(base64))
        {
            return false;
        }

        var buffer = new byte[SizeInBytes + 3];
        if (!Convert.TryFromBase64String(base64.Trim(), buffer, out var written) || written != SizeInBytes)
        {
            return false;
        }

        masterKey = new MasterKey(buffer[..SizeInBytes]);
        return true;
    }

    /// <exception cref="InvalidOperationException">The value is missing or not Base64 of 32 bytes.</exception>
    public static MasterKey Parse(string? base64) =>
        TryParse(base64, out var masterKey) ? masterKey : throw new InvalidOperationException(InvalidMessage);

    /// <summary>A 32-byte key for <paramref name="label"/> (one of the <see cref="KeyDerivation"/> labels).</summary>
    public byte[] DeriveKey(string label) => KeyDerivation.DeriveKey(_key, label);

    public override string ToString() => "MasterKey(redacted)";
}
