using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VehicleGo.Domain.Users;
using VehicleGo.Domain.Vehicles;

namespace VehicleGo.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>vehicles</c> (design §1.6). The check constraints are generated from the Domain code tables, so the
/// database accepts exactly the vehicle type → bracket combinations of <see cref="DisplacementBrackets"/>.
/// </summary>
public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("vehicles", t =>
        {
            t.HasCheckConstraint("ck_vehicles_type", $"vehicle_type IN ({CodeEnum<VehicleType>.SqlList(Enum.GetValues<VehicleType>())})");
            t.HasCheckConstraint("ck_vehicles_bracket", BracketCheckSql());
            t.HasCheckConstraint("ck_vehicles_dates", "original_issue_date >= manufacture_date");
            t.HasCheckConstraint("ck_vehicles_licence_date", LicenceDateCheckSql());
            t.HasCheckConstraint("ck_vehicles_plate_length", $"char_length(plate) BETWEEN 1 AND {VehicleLimits.PlateMaxLength}");
            t.HasCheckConstraint("ck_vehicles_normalized_plate_length", "char_length(normalized_plate) >= 1");
            t.HasCheckConstraint("ck_vehicles_brand_length", $"char_length(brand) BETWEEN 1 AND {VehicleLimits.TextMaxLength}");
            t.HasCheckConstraint("ck_vehicles_model_length", $"char_length(model) BETWEEN 1 AND {VehicleLimits.TextMaxLength}");
            t.HasCheckConstraint("ck_vehicles_nickname_length", $"nickname IS NULL OR char_length(nickname) <= {VehicleLimits.TextMaxLength}");
            t.HasCheckConstraint("ck_vehicles_color_length", $"color IS NULL OR char_length(color) <= {VehicleLimits.TextMaxLength}");
        });

        builder.HasKey(v => v.Id).HasName("pk_vehicles");
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.OwnerId).IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(v => v.OwnerId).IsRequired().HasConstraintName("fk_vehicles_owner");

        builder.Property(v => v.Plate).IsRequired();
        builder.Property(v => v.NormalizedPlate).IsRequired();
        builder.HasIndex(v => new { v.OwnerId, v.NormalizedPlate }).IsUnique().HasDatabaseName("ux_vehicles_owner_plate");

        builder.Property(v => v.Brand).IsRequired();
        builder.Property(v => v.Model).IsRequired();
        builder.Property(v => v.VehicleType).IsRequired();

        builder.HasIndex(v => new { v.OwnerId, v.DeletedAt }).HasDatabaseName("ix_vehicles_owner_deleted");

        builder.Property(v => v.CreatedAt).IsRequired();
        builder.Property(v => v.UpdatedAt).IsRequired();

        // PostgreSQL system column xmin as the concurrency token (no column is created by migrations).
        builder.Property(v => v.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        builder.Ignore(v => v.IsTrashed);
    }

    /// <summary>
    /// ELECTRIC_MC: null; every other type: one of its own brackets (Technical Plan §2 table). The explicit
    /// <c>IS NOT NULL</c> matters: <c>NULL IN (...)</c> is NULL, and a check constraint accepts NULL.
    /// </summary>
    private static string BracketCheckSql() =>
        string.Join(" OR ", DisplacementBrackets.VehicleTypes.Select(info =>
        {
            var type = $"vehicle_type = '{CodeEnum<VehicleType>.ToCode(info.Type)}'";
            return info.BracketRequired
                ? $"({type} AND displacement_bracket IS NOT NULL AND displacement_bracket IN ({CodeEnum<DisplacementBracket>.SqlList(info.Brackets)}))"
                : $"({type} AND displacement_bracket IS NULL)";
        }));

    private static string LicenceDateCheckSql() =>
        "licence_inspection_date IS NULL OR vehicle_type IN ("
        + CodeEnum<VehicleType>.SqlList(DisplacementBrackets.VehicleTypes.Where(t => t.LicenceDateAllowed).Select(t => t.Type))
        + ")";
}
