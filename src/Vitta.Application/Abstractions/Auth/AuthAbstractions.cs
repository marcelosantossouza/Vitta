using Vitta.Domain.Users;

namespace Vitta.Application.Abstractions.Auth;

public interface IPasswordHasher
{
    string Hash(User user, string password);
    bool Verify(User user, string hash, string password);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken Create(User user);
}

/// <summary>Usuario autenticado na requisicao atual (implementado na API a partir do HttpContext).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
}
