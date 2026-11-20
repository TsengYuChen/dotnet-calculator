using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using VehicleGo.Infrastructure.Crypto;

namespace VehicleGo.Infrastructure.DataProtection;

/// <summary>
/// Encrypts the Data Protection key ring XML with AES-GCM (key <c>dataprotection/v1</c>, purpose <c>dpkey:v1</c>).
/// Registered through <c>KeyManagementOptions.XmlEncryptor</c>.
/// </summary>
public sealed class AesGcmXmlEncryptor(MasterKey masterKey) : IXmlEncryptor
{
    internal const string EncryptedElementName = "encryptedKey";
    internal const string ValueElementName = "value";

    private readonly AesGcmProtector _protector = new(masterKey.DeriveKey(KeyDerivation.DataProtectionLabel));

    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);

        var plaintext = Encoding.UTF8.GetBytes(plaintextElement.ToString(SaveOptions.DisableFormatting));
        var element = new XElement(EncryptedElementName,
            new XComment(" AES-256-GCM, key derived from MASTER_KEY (dataprotection/v1). "),
            new XElement(ValueElementName, Convert.ToBase64String(_protector.Protect(plaintext, AesGcmProtector.DataProtectionKeyPurpose))));
        return new EncryptedXmlInfo(element, typeof(AesGcmXmlDecryptor));
    }
}
