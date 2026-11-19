using Microsoft.EntityFrameworkCore;

namespace Vitta.Persistence.Context;

/// <summary>PostgreSQL - banco de producao.</summary>
public sealed class PostgresVittaDbContext(DbContextOptions<PostgresVittaDbContext> options) : VittaDbContext(options)
{
    public const string Schema = "vitta";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
    }
}
