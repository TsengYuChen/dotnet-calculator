using System.Security.Cryptography;
using System.Text;
using VehicleGo.Infrastructure.Crypto;

namespace VehicleGo.UnitTests.Infrastructure;

/// <summary>Design §2.4: version 0x01 + nonce (12) + tag (16) + ciphertext; associated data = purpose.</summary>
public class AesGcmProtectorTests
{
    private static readonly byte[] Plaintext = Encoding.UTF8.GetBytes("alice@example.com");

    private static byte[] NewKey() => RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void 加解密往返()
    {
        var protector = new AesGcmProtector(NewKey());

        var data = protector.Protect(Plaintext, AesGcmProtector.EmailPurpose);

        Assert.Equal(Plaintext, protector.Unprotect(data, AesGcmProtector.EmailPurpose));
    }

    [Fact]
    public void 格式是版本byte_nonce_tag_密文()
    {
        var data = new AesGcmProtector(NewKey()).Protect(Plaintext, AesGcmProtector.EmailPurpose);

        Assert.Equal(1 + 12 + 16 + Plaintext.Length, data.Length);
        Assert.Equal(0x01, data[0]);
        Assert.Equal(29, AesGcmProtector.HeaderSize);
    }

    [Fact]
    public void 每次加密使用新的nonce()
    {
        var protector = new AesGcmProtector(NewKey());

        var first = protector.Protect(Plaintext, AesGcmProtector.EmailPurpose);
        var second = protector.Protect(Plaintext, AesGcmProtector.EmailPurpose);

        Assert.NotEqual(first.AsSpan(1, 12).ToArray(), second.AsSpan(1, 12).ToArray());
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void 存的不是明文()
    {
        var data = new AesGcmProtector(NewKey()).Protect(Plaintext, AesGcmProtector.EmailPurpose);

        Assert.Equal(-1, data.AsSpan().IndexOf(Plaintext));
        Assert.DoesNotContain("alice", Encoding.UTF8.GetString(data), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void 用錯金鑰解密失敗()
    {
        var data = new AesGcmProtector(NewKey()).Protect(Plaintext, AesGcmProtector.EmailPurpose);

        Assert.ThrowsAny<CryptographicException>(() => new AesGcmProtector(NewKey()).Unprotect(data, AesGcmProtector.EmailPurpose));
    }

    [Fact]
    public void 用途字串不同時解密失敗()
    {
        var protector = new AesGcmProtector(NewKey());
        var data = protector.Protect(Plaintext, AesGcmProtector.EmailPurpose);

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(data, AesGcmProtector.DataProtectionKeyPurpose));
    }

    [Theory]
    [InlineData(0)]   // version byte
    [InlineData(5)]   // nonce
    [InlineData(20)]  // tag
    [InlineData(-1)]  // last ciphertext byte
    public void 資料被竄改時解密失敗(int index)
    {
        var protector = new AesGcmProtector(NewKey());
        var data = protector.Protect(Plaintext, AesGcmProtector.EmailPurpose);
        data[index < 0 ? data.Length + index : index] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(data, AesGcmProtector.EmailPurpose));
    }

    [Fact]
    public void 太短的資料解密失敗()
    {
        var protector = new AesGcmProtector(NewKey());

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(new byte[AesGcmProtector.HeaderSize - 1], AesGcmProtector.EmailPurpose));
    }

    [Fact]
    public void 空的明文也能往返()
    {
        var protector = new AesGcmProtector(NewKey());

        var data = protector.Protect([], AesGcmProtector.DataProtectionKeyPurpose);

        Assert.Equal(AesGcmProtector.HeaderSize, data.Length);
        Assert.Empty(protector.Unprotect(data, AesGcmProtector.DataProtectionKeyPurpose));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public void 金鑰必須是32_bytes(int size) =>
        Assert.Throws<ArgumentException>(() => new AesGcmProtector(new byte[size]));

    [Fact]
    public void 用途字串是設計指定的值()
    {
        Assert.Equal("email:v1", AesGcmProtector.EmailPurpose);
        Assert.Equal("dpkey:v1", AesGcmProtector.DataProtectionKeyPurpose);
    }
}
