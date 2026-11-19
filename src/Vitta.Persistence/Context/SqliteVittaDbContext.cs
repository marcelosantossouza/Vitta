using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Vitta.Persistence.Context;

/// <summary>SQLite - banco local de desenvolvimento (arquivo vitta.db).</summary>
public sealed class SqliteVittaDbContext(DbContextOptions<SqliteVittaDbContext> options) : VittaDbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite nao sabe ordenar/comparar DateTimeOffset nem somar decimal em SQL.
        // Convertemos so aqui; Postgres continua com os tipos nativos (timestamptz / numeric).
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        configurationBuilder.Properties<decimal>().HaveConversion<double>();
    }
}
