using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vitta.Domain.Settings;

namespace Vitta.Persistence.Configurations;

internal sealed class BrokerSiteConfiguration : IEntityTypeConfiguration<BrokerSite>
{
    public void Configure(EntityTypeBuilder<BrokerSite> b)
    {
        b.ToTable("broker_site");
        b.HasKey(x => x.Id);
        b.Property(x => x.Host).HasMaxLength(120);
        b.Property(x => x.About).HasMaxLength(2000);
        b.Property(x => x.Address).HasMaxLength(250);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Hours).HasMaxLength(120);
        b.Property(x => x.Instagram).HasMaxLength(200);
        b.Property(x => x.Facebook).HasMaxLength(200);
        b.Property(x => x.LinkedIn).HasMaxLength(200);
        b.Property(x => x.Susep).HasMaxLength(30);
        b.Property(x => x.Cnpj).HasMaxLength(20);
        b.Property(x => x.ProductsText).HasMaxLength(1100);
        b.Property(x => x.AccentColor).HasMaxLength(7).IsRequired();
        b.Ignore(x => x.Products);
        b.Ignore(x => x.DomainEvents);
    }
}

internal sealed class SiteImageConfiguration : IEntityTypeConfiguration<SiteImage>
{
    public void Configure(EntityTypeBuilder<SiteImage> b)
    {
        b.ToTable("site_images");
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasMaxLength(10).IsRequired();
        b.HasIndex(x => x.Kind).IsUnique();
        b.Property(x => x.ContentType).HasMaxLength(30).IsRequired();
        b.Property(x => x.Data).IsRequired();
    }
}
