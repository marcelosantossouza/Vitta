using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vitta.Domain.Settings;

namespace Vitta.Persistence.Configurations;

internal sealed class NotificationSettingsConfiguration : IEntityTypeConfiguration<NotificationSettings>
{
    public void Configure(EntityTypeBuilder<NotificationSettings> b)
    {
        b.ToTable("notification_settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.RecipientList).HasMaxLength(4200).IsRequired();
        b.Ignore(x => x.Recipients);
        b.Ignore(x => x.DomainEvents);
    }
}
