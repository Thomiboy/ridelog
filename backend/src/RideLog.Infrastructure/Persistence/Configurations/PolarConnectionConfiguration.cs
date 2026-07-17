using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RideLog.Infrastructure.Persistence.Configurations;

internal sealed class PolarConnectionConfiguration : IEntityTypeConfiguration<PolarConnection>
{
    public void Configure(EntityTypeBuilder<PolarConnection> builder)
    {
        builder.HasKey(connection => connection.Id);

        builder.Property(connection => connection.UserId).IsRequired().HasMaxLength(450);
        builder.Property(connection => connection.PolarUserId).IsRequired().HasMaxLength(64);
        builder.Property(connection => connection.AccessTokenProtected).IsRequired();

        // One Polar connection per app user.
        builder.HasIndex(connection => connection.UserId).IsUnique();
    }
}
