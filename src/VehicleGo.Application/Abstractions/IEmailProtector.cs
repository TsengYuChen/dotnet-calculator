namespace VehicleGo.Application.Abstractions;

/// <summary>Protects stored e-mail addresses (implemented in Infrastructure).</summary>
public interface IEmailProtector
{
    /// <summary>Encrypts the e-mail for <c>users.email_encrypted</c>.</summary>
    byte[] Encrypt(string email);

    /// <summary><c>users.email_lookup_hash</c>: HMAC of the trimmed, lower-cased e-mail.</summary>
    byte[] ComputeLookupHash(string email);
}
