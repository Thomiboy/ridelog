using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RideLog.Application.Contact;
using RideLog.Domain.Contact;

namespace RideLog.Infrastructure.Persistence.Configurations;

internal sealed class ContactMessageConfiguration : IEntityTypeConfiguration<ContactMessage>
{
    public void Configure(EntityTypeBuilder<ContactMessage> builder)
    {
        builder.HasKey(message => message.Id);

        // The same caps the endpoint enforces, so a message that reached the row already fits it.
        builder.Property(message => message.SenderName).HasMaxLength(ContactLimits.NameMax);
        builder.Property(message => message.SenderEmail).HasMaxLength(ContactLimits.EmailMax);
        builder.Property(message => message.Body).HasMaxLength(ContactLimits.MessageMax);
    }
}
