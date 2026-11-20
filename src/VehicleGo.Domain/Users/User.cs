namespace VehicleGo.Domain.Users;

/// <summary>A user identified by Google <c>sub</c>; the e-mail is stored only encrypted plus a lookup hash.</summary>
public sealed class User
{
    private User()
    {
        GoogleSub = string.Empty;
        EmailEncrypted = [];
        EmailLookupHash = [];
    }

    public Guid Id { get; private set; }
    public string GoogleSub { get; private set; }
    public byte[] EmailEncrypted { get; private set; }
    public byte[] EmailLookupHash { get; private set; }
    public CalendarPreference CalendarPreference { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static User Create(Guid id, string googleSub, byte[] emailEncrypted, byte[] emailLookupHash, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(googleSub);
        ArgumentNullException.ThrowIfNull(emailEncrypted);
        ArgumentNullException.ThrowIfNull(emailLookupHash);
        return new User
        {
            Id = id,
            GoogleSub = googleSub,
            EmailEncrypted = emailEncrypted,
            EmailLookupHash = emailLookupHash,
            CalendarPreference = CalendarPreference.Roc,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void ChangeCalendarPreference(CalendarPreference preference, DateTimeOffset now)
    {
        if (!Enum.IsDefined(preference))
        {
            throw new ArgumentOutOfRangeException(nameof(preference));
        }

        CalendarPreference = preference;
        UpdatedAt = now;
    }
}
