using System.Security.Cryptography;
using System.Text;

namespace VehicleGo.Infrastructure.Crypto;

/// <summary>
/// AES-256-GCM (design §2.4). Format: version byte <c>0x01</c> + nonce (12 bytes) + tag (16 bytes) + ciphertext.
/// A new random nonce for every encryption; the associated data is the purpose string.
/// </summary>
public sealed class AesGcmProtector
{
    public const byte FormatVersion = 0x01;
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int HeaderSize = 1 + NonceSize + TagSize;

    /// <summary>Associated data for <c>users.email_encrypted</c>.</summary>
    public const string EmailPurpose = "email:v1";

    /// <summary>Associated data for the Data Protection key ring XML.</summary>
    public const string DataProtectionKeyPurpose = "dpkey:v1";

    private readonly byte[] _key;

    public AesGcmProtector(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != KeyDerivation.KeySizeInBytes)
        {
            throw new ArgumentException("The AES-GCM key must be 32 bytes.", nameof(key));
        }

        _key = (byte[])key.Clone();
    }

    public byte[] Protect(ReadOnlySpan<byte> plaintext, string purpose)
    {
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        var output = new byte[HeaderSize + plaintext.Length];
        output[0] = FormatVersion;
        var nonce = output.AsSpan(1, NonceSize);
        var tag = output.AsSpan(1 + NonceSize, TagSize);
        var ciphertext = output.AsSpan(HeaderSize);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes(purpose));
        return output;
    }

    /// <exception cref="CryptographicException">Unknown format, wrong key, wrong purpose or tampered data.</exception>
    public byte[] Unprotect(ReadOnlySpan<byte> protectedData, string purpose)
    {
        ArgumentException.ThrowIfNullOrEmpty(purpose);
        if (protectedData.Length < HeaderSize)
        {
            throw new CryptographicException("The protected data is too short.");
        }

        if (protectedData[0] != FormatVersion)
        {
            throw new CryptographicException("Unknown protected data format version.");
        }

        var nonce = protectedData.Slice(1, NonceSize);
        var tag = protectedData.Slice(1 + NonceSize, TagSize);
        var ciphertext = protectedData[HeaderSize..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, Encoding.UTF8.GetBytes(purpose));
        return plaintext;
    }
}
