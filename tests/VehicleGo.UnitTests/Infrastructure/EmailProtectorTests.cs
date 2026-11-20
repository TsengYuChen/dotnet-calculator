using System.Security.Cryptography;
using System.Text;
using VehicleGo.Infrastructure.Crypto;

namespace VehicleGo.UnitTests.Infrastructure;

public class EmailProtectorTests
{
    private static MasterKey NewMasterKey() => MasterKey.FromBytes(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void 加密後能解密回原本的Email()
    {
        var protector = new EmailProtector(NewMasterKey());

        var encrypted = protector.Encrypt("alice@example.com");

        Assert.Equal("alice@example.com", protector.Decrypt(encrypted));
        Assert.DoesNotContain("alice", Encoding.UTF8.GetString(encrypted), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void 用Email加密金鑰和email用途字串加密()
    {
        var master = NewMasterKey();

        var encrypted = new EmailProtector(master).Encrypt("alice@example.com");

        var aes = new AesGcmProtector(master.DeriveKey(KeyDerivation.EmailEncryptionLabel));
        Assert.Equal("alice@example.com", Encoding.UTF8.GetString(aes.Unprotect(encrypted, AesGcmProtector.EmailPurpose)));
    }

    [Fact]
    public void 查詢雜湊是HMAC_SHA256_去空白轉小寫()
    {
        var master = NewMasterKey();
        var protector = new EmailProtector(master);

        var expected = HMACSHA256.HashData(master.DeriveKey(KeyDerivation.EmailLookupHashLabel), Encoding.UTF8.GetBytes("alice@example.com"));

        Assert.Equal(expected, protector.ComputeLookupHash("alice@example.com"));
        Assert.Equal(expected, protector.ComputeLookupHash("  Alice@Example.COM \t"));
        Assert.Equal(32, expected.Length);
    }

    [Fact]
    public void 不同的主金鑰查詢雜湊不同() =>
        Assert.NotEqual(
            new EmailProtector(NewMasterKey()).ComputeLookupHash("alice@example.com"),
            new EmailProtector(NewMasterKey()).ComputeLookupHash("alice@example.com"));
}
