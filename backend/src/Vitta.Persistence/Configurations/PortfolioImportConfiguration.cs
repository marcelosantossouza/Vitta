using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vitta.Domain.Insurers;
using Vitta.Domain.Portfolio;

namespace Vitta.Persistence.Configurations;

internal sealed class PortfolioImportConfiguration : IEntityTypeConfiguration<PortfolioImport>
{
    public void Configure(EntityTypeBuilder<PortfolioImport> b)
    {
        b.ToTable("portfolio_imports");
        b.HasKey(x => x.Id);
        b.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.InsurerId, x.ReferenceMonth }).IsUnique();
        b.HasOne<Insurer>().WithMany().HasForeignKey(x => x.InsurerId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.DomainEvents);

        b.OwnsMany(x => x.Changes, c =>
        {
            c.ToTable("portfolio_changes");
            c.WithOwner().HasForeignKey("PortfolioImportId");
            c.HasKey(x => x.Id);
            c.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            c.Property(x => x.PolicyNumber).HasMaxLength(50).IsRequired();
            c.Property(x => x.HolderName).HasMaxLength(200).IsRequired();
            c.Property(x => x.HolderCpf).HasMaxLength(11);
            c.Property(x => x.Detail).HasMaxLength(500);
        });
        b.Navigation(x => x.Changes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
