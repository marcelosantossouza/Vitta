using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vitta.Domain.Clients;
using Vitta.Domain.Common;

namespace Vitta.Persistence.Configurations;

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> b)
    {
        b.ToTable("clients");
        b.HasKey(x => x.Id);
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.SearchName).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.SearchName);
        b.Property(x => x.Cpf)
            .HasConversion(cpf => cpf.Value, value => Cpf.Create(value).Value)
            .HasMaxLength(11)
            .IsRequired();
        b.HasIndex(x => x.Cpf).IsUnique();
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.HasIndex(x => x.ReferralGroupId);
        b.Ignore(x => x.DomainEvents);
    }
}
