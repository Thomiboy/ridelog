using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RideLog.Domain.Users;

namespace RideLog.Infrastructure.Persistence.Configurations;

internal sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        // One settings row per user; the UserId is the key. 450 matches the Identity user key length.
        builder.HasKey(settings => settings.UserId);
        builder.Property(settings => settings.UserId).HasMaxLength(450);
    }
}
