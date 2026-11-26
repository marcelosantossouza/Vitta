using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vitta.Domain.Clients;
using Vitta.Domain.Contacts;
using Vitta.Domain.Prospects;

namespace Vitta.Persistence.Configurations;

internal sealed class ContactLogConfiguration : IEntityTypeConfiguration<ContactLog>
{
    public void Configure(EntityTypeBuilder<ContactLog> b)
    {
        b.ToTable("contact_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Summary).HasMaxLength(ContactLog.MaxSummary).IsRequired();
        b.Property(x => x.UserName).HasMaxLength(120);
        b.Property(x => x.Channel).HasConversion<int>();
        // Excluir o prospect ou o cliente apaga o historico dele (LGPD: nao sobra dado solto).
        b.HasOne<Prospect>().WithMany().HasForeignKey(x => x.ProspectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProspectId, x.OccurredAt });
        b.HasIndex(x => new { x.ClientId, x.OccurredAt });
        b.Ignore(x => x.DomainEvents);
    }
}
