using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Vitta.Persistence.Context;

// Usadas apenas pelo `dotnet ef` para gerar migrations de cada provedor,
// independente de qual banco a API estiver configurada para usar.

internal sealed class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteVittaDbContext>
{
    public SqliteVittaDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqliteVittaDbContext>().UseSqlite("Data Source=design.db").Options);
}

internal sealed class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresVittaDbContext>
{
    public PostgresVittaDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PostgresVittaDbContext>()
            .UseNpgsql("Host=localhost;Database=vitta_design", DependencyInjection.ConfigureNpgsql)
            .Options);
}
