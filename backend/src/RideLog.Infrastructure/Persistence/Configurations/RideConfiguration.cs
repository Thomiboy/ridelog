using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RideLog.Domain.Rides;

namespace RideLog.Infrastructure.Persistence.Configurations;

internal sealed class RideConfiguration : IEntityTypeConfiguration<Ride>
{
    public void Configure(EntityTypeBuilder<Ride> builder)
    {
        builder.HasKey(ride => ride.Id);

        // 450 matches the key length of ASP.NET Core Identity users.
        builder.Property(ride => ride.UserId).IsRequired().HasMaxLength(450);
        builder.Property(ride => ride.Sport).IsRequired().HasMaxLength(64);
        builder.Property(ride => ride.Source).HasConversion<string>().HasMaxLength(16);

        // Uniqueness half of the duplicate guard: the same source event can never be stored
        // twice; cross-source duplicates are caught by the Ride.Overlaps matching contract.
        builder.HasIndex(ride => new { ride.UserId, ride.StartTime }).IsUnique();

        builder.HasMany(ride => ride.RawFiles)
            .WithOne()
            .HasForeignKey(file => file.RideId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(ride => ride.RawFiles).AutoInclude(false);
    }
}
