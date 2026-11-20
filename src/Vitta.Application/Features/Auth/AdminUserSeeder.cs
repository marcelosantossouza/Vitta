using Microsoft.Extensions.Logging;
using Vitta.Application.Abstractions.Auth;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Users;

namespace Vitta.Application.Features.Auth;

public sealed class SeedAdminOptions
{
    public const string SectionName = "Auth:SeedAdmin";

    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Password { get; init; }
}

/// <summary>Cria o primeiro usuario se o banco ainda nao tiver nenhum (dados vindos da configuracao).</summary>
public sealed class AdminUserSeeder(IUserRepository users, IPasswordHasher hasher, IUnitOfWork uow, ILogger<AdminUserSeeder> logger)
{
    public async Task SeedAsync(SeedAdminOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password) || await users.AnyAsync(ct))
        {
            return;
        }

        var user = User.Create(options.Name ?? "Administrador", options.Email).Value;
        user.SetPasswordHash(hasher.Hash(user, options.Password));
        users.Add(user);
        await uow.SaveChangesAsync(ct);
        logger.LogInformation("Usuario inicial {Email} criado.", user.Email);
    }
}
