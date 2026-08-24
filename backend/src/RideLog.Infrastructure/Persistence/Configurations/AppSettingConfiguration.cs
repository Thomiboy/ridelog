using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RideLog.Infrastructure.Persistence.Configurations;

internal sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        // Named for the table convention the other rows follow; the entity is reached through Set<>()
        // rather than a DbSet, so nothing else supplies the name.
        builder.ToTable("AppSettings");

        // The key is the primary key; one row per setting. 200 is generous for the short, code-owned
        // keys this holds (e.g. "public-log-rider-id").
        builder.HasKey(setting => setting.Key);
        builder.Property(setting => setting.Key).HasMaxLength(200);
    }
}
