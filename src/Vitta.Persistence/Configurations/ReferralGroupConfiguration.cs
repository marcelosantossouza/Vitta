using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vitta.Domain.Referrals;

namespace Vitta.Persistence.Configurations;

internal sealed class ReferralGroupConfiguration : IEntityTypeConfiguration<ReferralGroup>
{
    public void Configure(EntityTypeBuilder<ReferralGroup> b)
    {
        b.ToTable("referral_groups");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Code).HasMaxLength(30);
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.SharePercent).HasPrecision(5, 2);
        b.Property(x => x.ShareLabel).HasMaxLength(40).IsRequired();
        b.Ignore(x => x.DomainEvents);
    }
}
