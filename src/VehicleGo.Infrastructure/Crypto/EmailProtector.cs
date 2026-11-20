using System.Security.Cryptography;
using System.Text;
using VehicleGo.Application.Abstractions;

namespace VehicleGo.Infrastructure.Crypto;

/// <summary>
/// <see cref="IEmailProtector"/>: AES-GCM with the <c>email-enc/v1</c> key; lookup hash =
/// HMAC-SHA256(<c>email-hmac/v1</c> key, trimmed and lower-cased e-mail).
/// </summary>
public sealed class EmailProtector(MasterKey masterKey) : IEmailProtector
{
    private readonly AesGcmProtector _protector = new(masterKey.DeriveKey(KeyDerivation.EmailEncryptionLabel));
    private readonly byte[] _hashKey = masterKey.DeriveKey(KeyDerivation.EmailLookupHashLabel);

    public byte[] Encrypt(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return _protector.Protect(Encoding.UTF8.GetBytes(email), AesGcmProtector.EmailPurpose);
    }

    /// <summary>For manual deletion requests only; the application never needs the stored e-mail.</summary>
    public string Decrypt(byte[] emailEncrypted)
    {
        ArgumentNullException.ThrowIfNull(emailEncrypted);
        return Encoding.UTF8.GetString(_protector.Unprotect(emailEncrypted, AesGcmProtector.EmailPurpose));
    }

    public byte[] ComputeLookupHash(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return HMACSHA256.HashData(_hashKey, Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
    }
}
