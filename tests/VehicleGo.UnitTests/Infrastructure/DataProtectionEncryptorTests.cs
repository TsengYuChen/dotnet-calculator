using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VehicleGo.Infrastructure.Crypto;
using VehicleGo.Infrastructure.DataProtection;
using VehicleGo.UnitTests.Fakes;

namespace VehicleGo.UnitTests.Infrastructure;

/// <summary>Design §2.4: the Data Protection key ring XML is encrypted with AES-GCM (key <c>dataprotection/v1</c>).</summary>
public class DataProtectionEncryptorTests
{
    /// <summary>A recognisable plaintext marker (not key material).</summary>
    private const string MarkerValue = "plaintext-marker-for-tests";

    private static XElement KeyXml() =>
        new("key",
            new XAttribute("id", "6f1a9c4e-0000-0000-0000-000000000001"),
            new XElement("descriptor", new XElement("masterKey", new XElement("value", MarkerValue))));

    private static MasterKey NewMasterKey() => MasterKey.FromBytes(RandomNumberGenerator.GetBytes(32));

    private static (IServiceProvider Services, CapturingLogger<AesGcmXmlDecryptor> Logger) Services(MasterKey masterKey)
    {
        var logger = new CapturingLogger<AesGcmXmlDecryptor>();
        var services = new ServiceCollection()
            .AddSingleton(masterKey)
            .AddSingleton<ILogger<AesGcmXmlDecryptor>>(logger)
            .BuildServiceProvider();
        return (services, logger);
    }

    /// <summary>The framework creates decryptors with <c>Activator</c> and the <c>(IServiceProvider)</c> constructor.</summary>
    private static AesGcmXmlDecryptor CreateDecryptor(IServiceProvider services) =>
        (AesGcmXmlDecryptor)Activator.CreateInstance(typeof(AesGcmXmlDecryptor), services)!;

    [Fact]
    public void 加解密往返()
    {
        var master = NewMasterKey();
        var info = new AesGcmXmlEncryptor(master).Encrypt(KeyXml());

        var decrypted = CreateDecryptor(Services(master).Services).Decrypt(info.EncryptedElement);

        Assert.True(XNode.DeepEquals(KeyXml(), decrypted));
    }

    [Fact]
    public void 加密結果指定AesGcmXmlDecryptor()
    {
        var info = new AesGcmXmlEncryptor(NewMasterKey()).Encrypt(KeyXml());

        Assert.Equal(typeof(AesGcmXmlDecryptor), info.DecryptorType);
    }

    [Fact]
    public void 存的不是明文()
    {
        var info = new AesGcmXmlEncryptor(NewMasterKey()).Encrypt(KeyXml());
        var stored = info.EncryptedElement.ToString();

        Assert.DoesNotContain(MarkerValue, stored, StringComparison.Ordinal);
        Assert.DoesNotContain("masterKey", stored, StringComparison.Ordinal);
    }

    [Fact]
    public void 用錯金鑰解密失敗並寫Error_log()
    {
        var info = new AesGcmXmlEncryptor(NewMasterKey()).Encrypt(KeyXml());
        var (services, logger) = Services(NewMasterKey());

        Assert.ThrowsAny<CryptographicException>(() => CreateDecryptor(services).Decrypt(info.EncryptedElement));
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    [Fact]
    public void 用途字串不同時解密失敗()
    {
        var master = NewMasterKey();
        var info = new AesGcmXmlEncryptor(master).Encrypt(KeyXml());

        // Same data protection key, but encrypted for the e-mail purpose.
        var aes = new AesGcmProtector(master.DeriveKey(KeyDerivation.DataProtectionLabel));
        var wrongPurpose = aes.Protect(Encoding.UTF8.GetBytes(KeyXml().ToString()), AesGcmProtector.EmailPurpose);
        var element = new XElement(info.EncryptedElement);
        element.Descendants().Single(e => e.Name.LocalName == "value").Value = Convert.ToBase64String(wrongPurpose);

        Assert.ThrowsAny<CryptographicException>(() => CreateDecryptor(Services(master).Services).Decrypt(element));
    }

    [Fact]
    public void 用Email的金鑰解密失敗()
    {
        var master = NewMasterKey();

        // Right purpose, but the e-mail encryption key instead of the data protection key.
        var aes = new AesGcmProtector(master.DeriveKey(KeyDerivation.EmailEncryptionLabel));
        var data = aes.Protect(Encoding.UTF8.GetBytes(KeyXml().ToString()), AesGcmProtector.DataProtectionKeyPurpose);
        var element = new AesGcmXmlEncryptor(master).Encrypt(KeyXml()).EncryptedElement;
        element.Descendants().Single(e => e.Name.LocalName == "value").Value = Convert.ToBase64String(data);

        Assert.ThrowsAny<CryptographicException>(() => CreateDecryptor(Services(master).Services).Decrypt(element));
    }

    [Fact]
    public void 解密器可以用IServiceProvider建構式建立()
    {
        var constructor = typeof(AesGcmXmlDecryptor).GetConstructor([typeof(IServiceProvider)]);

        Assert.NotNull(constructor);
        Assert.NotNull(CreateDecryptor(Services(NewMasterKey()).Services));
    }

    [Fact]
    public void 沒有註冊logger時也能建立解密器()
    {
        var services = new ServiceCollection().AddSingleton(NewMasterKey()).BuildServiceProvider();

        Assert.NotNull(CreateDecryptor(services));
    }
}
