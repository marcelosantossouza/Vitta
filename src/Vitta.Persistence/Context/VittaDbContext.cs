using Microsoft.EntityFrameworkCore;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Clients;
using Vitta.Domain.Insurers;
using Vitta.Domain.Policies;
using Vitta.Domain.Portfolio;
using Vitta.Domain.Prospects;

namespace Vitta.Persistence.Context;

/// <summary>
/// Contexto base, independente de banco. Repositorios e o resto do sistema dependem SO deste tipo.
/// Cada banco tem uma subclasse (<see cref="SqliteVittaDbContext"/>, <see cref="PostgresVittaDbContext"/>)
/// apenas para que as migrations de cada provedor fiquem separadas (Migrations/Sqlite, Migrations/Postgres).
/// </summary>
public abstract class VittaDbContext(DbContextOptions options) : DbContext(options), IUnitOfWork
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Insurer> Insurers => Set<Insurer>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<Prospect> Prospects => Set<Prospect>();
    public DbSet<PortfolioImport> PortfolioImports => Set<PortfolioImport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VittaDbContext).Assembly);

    // Futuro: despachar DomainEvents dos AggregateRoots apos o SaveChanges (outbox ou in-process).
}
