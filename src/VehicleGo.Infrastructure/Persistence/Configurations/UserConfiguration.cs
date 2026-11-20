using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VehicleGo.Domain.Users;

namespace VehicleGo.Infrastructure.Persistence.Configurations;

/// <summary><c>users</c> (design §1.6). Code columns use the shared code converter (conventions).</summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("users", t => t.HasCheckConstraint(
            "ck_users_calendar",
            $"calendar_preference IN ({CodeEnum<CalendarPreference>.SqlList(Enum.GetValues<CalendarPreference>())})"));

        builder.HasKey(u => u.Id).HasName("pk_users");
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.GoogleSub).IsRequired();
        builder.HasIndex(u => u.GoogleSub).IsUnique().HasDatabaseName("ux_users_google_sub");

        builder.Property(u => u.EmailEncrypted).IsRequired();
        builder.Property(u => u.EmailLookupHash).IsRequired();
        builder.HasIndex(u => u.EmailLookupHash).HasDatabaseName("ix_users_email_lookup_hash");

        // The login upsert (raw SQL) leaves the column out and gets the database default.
        builder.Property(u => u.CalendarPreference).IsRequired().HasDefaultValue(CalendarPreference.Roc);

        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt).IsRequired();
    }
}
