using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RideLog.Domain.Rides;

namespace RideLog.Infrastructure.Persistence.Configurations;

internal sealed class RawFileConfiguration : IEntityTypeConfiguration<RawFile>
{
    public void Configure(EntityTypeBuilder<RawFile> builder)
    {
        builder.HasKey(file => file.Id);

        builder.Property(file => file.UserId).IsRequired().HasMaxLength(450);
        builder.Property(file => file.Format).HasConversion<string>().HasMaxLength(8);
        builder.Property(file => file.FileName).HasMaxLength(256);
        builder.Property(file => file.Content).IsRequired();

        builder.HasIndex(file => file.UserId);
    }
}
