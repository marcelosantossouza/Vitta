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
        b.Property(x => x.Phone2).HasMaxLength(30);
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.State).HasMaxLength(30);
        b.Property(x => x.ZipCode).HasMaxLength(20);
        b.Property(x => x.Occupation).HasMaxLength(150);
        b.Property(x => x.EstimatedSalary).HasPrecision(12, 2);
        b.Property(x => x.Discounts).HasPrecision(12, 2);
        b.Property(x => x.Loans).HasPrecision(12, 2);
        b.HasIndex(x => x.City);
        b.HasIndex(x => x.State);
        b.HasIndex(x => x.ImportId);
        b.HasOne<ProspectImport>().WithMany().HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.SetNull);
        b.Ignore(x => x.Profile);
        b.Ignore(x => x.IsUntouchedLead);
        b.Ignore(x => x.DomainEvents);
    }
}
