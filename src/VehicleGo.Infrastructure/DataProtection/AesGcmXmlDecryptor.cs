using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VehicleGo.Infrastructure.Crypto;

namespace VehicleGo.Infrastructure.DataProtection;

/// <summary>
/// Decrypts what <see cref="AesGcmXmlEncryptor"/> wrote. The framework creates it with the
/// <see cref="AesGcmXmlDecryptor(IServiceProvider)"/> constructor (not through DI).
/// </summary>
public sealed partial class AesGcmXmlDecryptor : IXmlDecryptor
{
    private readonly AesGcmProtector _protector;
    private readonly ILogger _logger;

    public AesGcmXmlDecryptor(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _protector = new AesGcmProtector(services.GetRequiredService<MasterKey>().DeriveKey(KeyDerivation.DataProtectionLabel));
        _logger = services.GetService<ILogger<AesGcmXmlDecryptor>>() ?? (ILogger)NullLogger.Instance;
    }

    public XElement Decrypt(XElement encryptedElement)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);
        try
        {
            var value = encryptedElement.Element(AesGcmXmlEncryptor.ValueElementName)?.Value
                ?? throw new CryptographicException("The encrypted key element has no value.");
            var plaintext = _protector.Unprotect(Convert.FromBase64String(value), AesGcmProtector.DataProtectionKeyPurpose);
            return XElement.Parse(Encoding.UTF8.GetString(plaintext));
        }
        catch (CryptographicException ex)
        {
            LogDecryptFailed(_logger, ex);
            throw;
        }
        catch (FormatException ex)
        {
            LogDecryptFailed(_logger, ex);
            throw new CryptographicException("The encrypted key element is malformed.", ex);
        }
    }

    [LoggerMessage(EventId = 1, EventName = "DATA_PROTECTION_KEY_DECRYPT_FAILED", Level = LogLevel.Error,
        Message = "DATA_PROTECTION_KEY_DECRYPT_FAILED: a Data Protection key could not be decrypted (wrong MASTER_KEY or corrupted key).")]
    private static partial void LogDecryptFailed(ILogger logger, Exception exception);
}
