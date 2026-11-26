using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Users;

namespace Vitta.API.Infrastructure;

/// <summary>Politicas de acesso por perfil. Sem politica, a rota so exige estar logado.</summary>
internal static class AuthPolicies
{
    public const string RoleClaim = "role";

    /// <summary>Usuarios e Configuracoes.</summary>
    public const string Admin = "Admin";

    /// <summary>Excluir registros (assistentes nao excluem).</summary>
    public const string CanDelete = "CanDelete";

    public static IServiceCollection AddVittaAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Admin, p => p.RequireRole(nameof(UserRole.Admin)))
            .AddPolicy(CanDelete, p => p.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Broker)));
        return services;
    }

    /// <summary>
    /// Depois de validar a assinatura do token, confere o usuario no banco: desativado perde o acesso na hora, e o
    /// perfil vale o atual (nao o de quando o token foi emitido). Uma consulta curta por requisicao.
    /// </summary>
    public static async Task OnTokenValidated(TokenValidatedContext context)
    {
        if (!Guid.TryParse(context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
        {
            context.Fail("Token sem usuario.");
            return;
        }

        var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        var user = await users.GetByIdAsync(userId, context.HttpContext.RequestAborted);
        if (user is null || !user.IsActive)
        {
            context.Fail("Usuario inexistente ou desativado.");
            return;
        }

        if (context.Principal!.Identity is ClaimsIdentity identity)
        {
            foreach (var claim in identity.FindAll(RoleClaim).ToList())
            {
                identity.RemoveClaim(claim);
            }

            identity.AddClaim(new Claim(RoleClaim, user.Role.ToString()));
        }
    }
}
