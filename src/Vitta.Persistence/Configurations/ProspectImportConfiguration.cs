using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vitta.Domain.Prospects;

namespace Vitta.Persistence.Configurations;

internal sealed class ProspectImportConfiguration : IEntityTypeConfiguration<ProspectImport>
{
    public void Configure(EntityTypeBuilder<ProspectImport> b)
    {
        b.ToTable("prospect_imports");
        b.HasKey(x => x.Id);
        b.Property(x => x.FileName).HasMaxLength(200).IsRequired();
        b.Property(x => x.TemplateName).HasMaxLength(120).IsRequired();
        b.Ignore(x => x.DomainEvents);
    }
}
