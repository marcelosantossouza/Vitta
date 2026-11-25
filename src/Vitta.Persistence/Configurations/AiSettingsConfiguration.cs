using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vitta.Domain.Settings;

namespace Vitta.Persistence.Configurations;

internal sealed class AiSettingsConfiguration : IEntityTypeConfiguration<AiSettings>
{
    public void Configure(EntityTypeBuilder<AiSettings> b)
    {
        b.ToTable("ai_settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Model).HasMaxLength(100).IsRequired();
        b.Property(x => x.Effort).HasMaxLength(10).IsRequired();
        b.Ignore(x => x.DomainEvents);
    }
}
