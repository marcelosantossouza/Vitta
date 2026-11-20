using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Vitta.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Auth:Jwt";

    public string Issuer { get; init; } = "vitta";
    public string Audience { get; init; } = "vitta-clients";
    /// <summary>Chave HMAC (minimo 32 caracteres). Em producao vem de variavel de ambiente/secret.</summary>
    public string Key { get; init; } = string.Empty;
    public int ExpiresHours { get; init; } = 12;

    public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(Key));
}
