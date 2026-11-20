using System.Security.Cryptography;
using System.Text;

namespace VehicleGo.Infrastructure.Crypto;

/// <summary>
/// HKDF-SHA256 key derivation from the master key (design §2.4): empty salt, info = UTF-8 label, 32 bytes.
/// </summary>
public static class KeyDerivation
{
    /// <summary>E-mail encryption (<c>users.email_encrypted</c>).</summary>
    public const string EmailEncryptionLabel = "email-enc/v1";

    /// <summary>E-mail lookup hash (<c>users.email_lookup_hash</c>, HMAC-SHA256).</summary>
    public const string EmailLookupHashLabel = "email-hmac/v1";

    /// <summary>Encryption of the Data Protection key ring XML.</summary>
    public const string DataProtectionLabel = "dataprotection/v1";

    public const int KeySizeInBytes = 32;

    public static byte[] DeriveKey(byte[] masterKey, string label)
    {
        ArgumentNullException.ThrowIfNull(masterKey);
        ArgumentException.ThrowIfNullOrEmpty(label);
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, KeySizeInBytes, salt: [], info: Encoding.UTF8.GetBytes(label));
    }
}
