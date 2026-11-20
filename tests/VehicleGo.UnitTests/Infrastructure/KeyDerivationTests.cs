using VehicleGo.Infrastructure.Crypto;

namespace VehicleGo.UnitTests.Infrastructure;

/// <summary>
/// HKDF-SHA256 (design §2.4) pinned with a fixed, obviously fake test vector: the master key is the bytes
/// 0x00..0x1f. This file is allowlisted in <c>.gitleaks.toml</c>; never put a real MASTER_KEY here.
/// Expected values were computed independently (RFC 5869, empty salt = 32 zero bytes, info = UTF-8 label).
/// </summary>
public class KeyDerivationTests
{
    /// <summary>Fake test vector: Base64 of bytes 0x00, 0x01, …, 0x1f. NOT a real key.</summary>
    private const string FakeMasterKeyBase64 = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    private static readonly byte[] FakeMasterKeyBytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    [Theory]
    [InlineData(KeyDerivation.EmailEncryptionLabel, "fad9de0c5dc92518ade8aed106a7ce9230ad9c3ea315e3f25ee958529abd8d09")]
    [InlineData(KeyDerivation.EmailLookupHashLabel, "472a854274abcf650b965eb8de8f4b09310a3c361853ce58c5a4059141a626ef")]
    [InlineData(KeyDerivation.DataProtectionLabel, "8b1e0d0209e0f12e5748ccfb2d29ccc9def4cae36a0581e920a9bf161814109b")]
    public void 固定測試向量的推導結果(string label, string expectedHex)
    {
        Assert.Equal(expectedHex, Convert.ToHexStringLower(KeyDerivation.DeriveKey(FakeMasterKeyBytes, label)));
        Assert.Equal(expectedHex, Convert.ToHexStringLower(MasterKey.Parse(FakeMasterKeyBase64).DeriveKey(label)));
    }

    [Fact]
    public void 標籤是設計指定的字串()
    {
        Assert.Equal("email-enc/v1", KeyDerivation.EmailEncryptionLabel);
        Assert.Equal("email-hmac/v1", KeyDerivation.EmailLookupHashLabel);
        Assert.Equal("dataprotection/v1", KeyDerivation.DataProtectionLabel);
    }

    [Fact]
    public void 三把金鑰都是32_bytes且互不相同()
    {
        var master = MasterKey.Parse(FakeMasterKeyBase64);
        var keys = new[] { KeyDerivation.EmailEncryptionLabel, KeyDerivation.EmailLookupHashLabel, KeyDerivation.DataProtectionLabel }
            .Select(master.DeriveKey)
            .ToList();

        Assert.All(keys, k => Assert.Equal(32, k.Length));
        Assert.Equal(3, keys.Select(Convert.ToHexString).Distinct().Count());
    }

    [Fact]
    public void 不同的主金鑰推導出不同的金鑰()
    {
        var other = (byte[])FakeMasterKeyBytes.Clone();
        other[0] ^= 0xff;

        Assert.NotEqual(
            KeyDerivation.DeriveKey(FakeMasterKeyBytes, KeyDerivation.EmailEncryptionLabel),
            KeyDerivation.DeriveKey(other, KeyDerivation.EmailEncryptionLabel));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not base64!")]
    [InlineData("AAECAwQFBgcICQoLDA0ODw==")] // 16 bytes
    [InlineData("AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8g")] // 33 bytes
    public void MASTER_KEY必須是32_bytes的Base64(string? value)
    {
        Assert.False(MasterKey.TryParse(value, out var key));
        Assert.Null(key);
        var error = Assert.Throws<InvalidOperationException>(() => MasterKey.Parse(value));
        Assert.Contains("MASTER_KEY", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 合法的MASTER_KEY_前後空白會被忽略()
    {
        Assert.True(MasterKey.TryParse($"  {FakeMasterKeyBase64}\n", out var key));
        Assert.Equal(
            KeyDerivation.DeriveKey(FakeMasterKeyBytes, KeyDerivation.DataProtectionLabel),
            key.DeriveKey(KeyDerivation.DataProtectionLabel));
    }

    [Fact]
    public void ToString不洩漏金鑰()
    {
        var text = MasterKey.Parse(FakeMasterKeyBase64).ToString();

        Assert.DoesNotContain(FakeMasterKeyBase64, text, StringComparison.Ordinal);
        Assert.DoesNotContain("AAECAw", text, StringComparison.Ordinal);
    }
}
