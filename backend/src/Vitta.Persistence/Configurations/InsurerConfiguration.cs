using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vitta.Domain.Insurers;

namespace Vitta.Persistence.Configurations;

internal sealed class InsurerConfiguration : IEntityTypeConfiguration<Insurer>
{
    public void Configure(EntityTypeBuilder<Insurer> b)
    {
        b.ToTable("insurers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Ignore(x => x.DomainEvents);

        var seededAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        b.HasData(
            Seed("0199a000-0000-7000-8000-000000000001", "MAG", "MAG Seguros (Mongeral Aegon)", seededAt),
            Seed("0199a000-0000-7000-8000-000000000002", "PRUDENTIAL", "Prudential do Brasil", seededAt),
            Seed("0199a000-0000-7000-8000-000000000003", "ICATU", "Icatu Seguros", seededAt),
            Seed("0199a000-0000-7000-8000-000000000004", "METLIFE", "MetLife", seededAt));
    }

    private static object Seed(string id, string code, string name, DateTimeOffset createdAt) =>
        new { Id = Guid.Parse(id), Code = code, Name = name, IsActive = true, CreatedAt = createdAt };
}
