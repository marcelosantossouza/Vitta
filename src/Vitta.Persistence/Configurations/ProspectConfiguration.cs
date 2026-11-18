using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vitta.Domain.Prospects;

namespace Vitta.Persistence.Configurations;

internal sealed class ProspectConfiguration : IEntityTypeConfiguration<Prospect>
{
    public void Configure(EntityTypeBuilder<Prospect> b)
    {
        b.ToTable("prospects");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Source).HasMaxLength(100);
        b.Property(x => x.Stage).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => x.Stage);
        b.Ignore(x => x.DomainEvents);
    }
}
