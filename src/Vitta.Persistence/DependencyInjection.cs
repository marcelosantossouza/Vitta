using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Persistence.Context;
using Vitta.Application.Abstractions.Queries;
using Vitta.Persistence.Queries;
using Vitta.Persistence.Repositories;

namespace Vitta.Persistence;

public static class DependencyInjection
{
    /// <param name="contentRootPath">Base para caminhos relativos do SQLite (ex.: "Data Source=vitta.db").</param>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        var options = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        services.AddSingleton(options);

        var connectionString = configuration.GetConnectionString("Vitta")
            ?? throw new InvalidOperationException("ConnectionStrings:Vitta nao configurada.");

        switch (options.Provider)
        {
            case DatabaseProvider.Sqlite:
                var sqlite = ResolveSqlitePath(connectionString, contentRootPath);
                services.AddDbContext<SqliteVittaDbContext>(o => o.UseSqlite(sqlite));
                services.AddScoped<VittaDbContext>(sp => sp.GetRequiredService<SqliteVittaDbContext>());
                break;

            case DatabaseProvider.Postgres:
                services.AddDbContext<PostgresVittaDbContext>(o => o.UseNpgsql(connectionString, ConfigureNpgsql));
                services.AddScoped<VittaDbContext>(sp => sp.GetRequiredService<PostgresVittaDbContext>());
                break;

            default:
                throw new InvalidOperationException($"Provedor de banco nao suportado: {options.Provider}");
        }

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<VittaDbContext>());

        // Escrita
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IPolicyRepository, PolicyRepository>();
        services.AddScoped<IProspectRepository, ProspectRepository>();
        services.AddScoped<IInsurerRepository, InsurerRepository>();
        services.AddScoped<IPortfolioImportRepository, PortfolioImportRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IImportTemplateRepository, ImportTemplateRepository>();
        services.AddScoped<IAiSettingsRepository, AiSettingsRepository>();

        // Leitura
        services.AddScoped<IClientQueries, ClientQueries>();
        services.AddScoped<IPolicyQueries, PolicyQueries>();
        services.AddScoped<IProspectQueries, ProspectQueries>();
        services.AddScoped<PortfolioQueries>();
        services.AddScoped<IPortfolioQueries>(sp => sp.GetRequiredService<PortfolioQueries>());
        services.AddScoped<IDashboardQueries, DashboardQueries>();
        services.AddScoped<IImportTemplateQueries, ImportTemplateQueries>();
        return services;
    }

    internal static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, PostgresVittaDbContext.Schema);

    private static string ResolveSqlitePath(string connectionString, string contentRootPath)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (!string.IsNullOrEmpty(builder.DataSource) && builder.DataSource != ":memory:" && !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.Combine(contentRootPath, builder.DataSource);
        }

        return builder.ToString();
    }
}
