using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vitta.Domain.Settings;

namespace Vitta.Persistence.Configurations;

internal sealed class LeadCaptureSettingsConfiguration : IEntityTypeConfiguration<LeadCaptureSettings>
{
    public void Configure(EntityTypeBuilder<LeadCaptureSettings> b)
    {
        b.ToTable("lead_capture_settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.BrokerName).HasMaxLength(120).IsRequired();
        b.Property(x => x.Headline).HasMaxLength(150).IsRequired();
        b.Property(x => x.WhatsApp).HasMaxLength(13);
        b.Ignore(x => x.ConsentText);
        b.Ignore(x => x.DomainEvents);
    }
}
