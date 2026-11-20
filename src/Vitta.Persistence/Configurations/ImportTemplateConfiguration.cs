using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vitta.Domain.Imports;

namespace Vitta.Persistence.Configurations;

internal sealed class ImportTemplateConfiguration : IEntityTypeConfiguration<ImportTemplate>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<ImportTemplate> b)
    {
        b.ToTable("import_templates");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.SheetName).HasMaxLength(120);
        b.Property(x => x.AiNotes).HasMaxLength(2000);
        b.HasIndex(x => new { x.Kind, x.Name }).IsUnique();
        b.HasOne<Domain.Insurers.Insurer>().WithMany().HasForeignKey(x => x.InsurerId).OnDelete(DeleteBehavior.SetNull);

        // Mapeamento de colunas guardado como JSON em texto: funciona igual no SQLite e no Postgres.
        b.Ignore(x => x.Columns);
        b.Property<List<ColumnMapping>>("_columns")
            .HasColumnName("Columns")
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<List<ColumnMapping>>(v, Json) ?? new List<ColumnMapping>(),
                new ValueComparer<List<ColumnMapping>>(
                    (a, c) => a!.SequenceEqual(c!),
                    v => v.Aggregate(0, (h, m) => HashCode.Combine(h, m.GetHashCode())),
                    v => v.ToList()));

        b.Ignore(x => x.DomainEvents);
    }
}
