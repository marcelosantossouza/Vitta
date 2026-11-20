using Vitta.Domain.Common;

namespace Vitta.Domain.Users;

/// <summary>Usuario do sistema (o corretor e, no futuro, assistentes).</summary>
public sealed class User : AggregateRoot
{
    private User() { } // EF

    private User(string name, string email)
    {
        Name = name;
        Email = email;
    }

    public string Name { get; private set; } = null!;
    /// <summary>Sempre em minusculas.</summary>
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public DateTimeOffset? LastLoginAt { get; private set; }

    public static Result<User> Create(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
        {
            return Error.Validation("User.Invalid", "Nome e e-mail são obrigatórios.");
        }

        return new User(name.Trim(), NormalizeEmail(email));
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public void SetPasswordHash(string hash)
    {
        PasswordHash = hash;
        Touch();
    }

    public void RegisterLogin() => LastLoginAt = DateTimeOffset.UtcNow;
}
