using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Persistence.Repositories;

namespace Vitta.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<VittaDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "vitta")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<VittaDbContext>());
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IInsurerRepository, InsurerRepository>();
        return services;
    }
}
