using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vitta.Domain.Clients;
using Vitta.Domain.Insurers;
using Vitta.Domain.Policies;

namespace Vitta.Persistence.Configurations;

internal sealed class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> b)
    {
        b.ToTable("policies");
        b.HasKey(x => x.Id);
        b.Property(x => x.PolicyNumber).HasMaxLength(50).IsRequired();
        b.HasIndex(x => new { x.InsurerId, x.PolicyNumber }).IsUnique();
        b.Property(x => x.ProductName).HasMaxLength(150);
        b.Property(x => x.MonthlyPremium).HasPrecision(12, 2);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.EndReason).HasMaxLength(500);
        b.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Insurer>().WithMany().HasForeignKey(x => x.InsurerId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.DomainEvents);
    }
}
