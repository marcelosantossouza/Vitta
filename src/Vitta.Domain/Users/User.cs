using Vitta.Domain.Common;

namespace Vitta.Domain.Users;

/// <summary>Perfil de acesso. A API confere o perfil em cada rota; o front so esconde o que nao se aplica.</summary>
public enum UserRole
{
    /// <summary>Tudo, inclusive usuarios e Configuracoes (IA, e-mail diario).</summary>
    Admin = 1,

    /// <summary>Dia a dia completo: clientes, apolices, importacoes, prospeccao (inclusive excluir).</summary>
    Broker = 2,

    /// <summary>Ve e cadastra; nao exclui nem mexe em configuracoes.</summary>
    Assistant = 3,
}

/// <summary>Usuario do sistema (o corretor, assistentes...).</summary>
public sealed class User : AggregateRoot
{
    public const int MinPasswordLength = 8;
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private User() { } // EF

    private User(string name, string email, UserRole role)
    {
        Name = name;
        Email = email;
        Role = role;
        IsActive = true;
    }

    public string Name { get; private set; } = null!;

    /// <summary>Sempre em minusculas.</summary>
    public string Email { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Senha provisoria (definida por um administrador): pede troca no proximo acesso.</summary>
    public bool MustChangePassword { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }

    public static Result<User> Create(string name, string email, UserRole role = UserRole.Admin)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
        {
            return Error.Validation("User.Invalid", "Nome e e-mail são obrigatórios.");
        }

        if (!Enum.IsDefined(role))
        {
            return UserErrors.InvalidRole;
        }

        return new User(name.Trim(), NormalizeEmail(email), role);
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static Result ValidatePassword(string? password) =>
        password is null || password.Length < MinPasswordLength ? UserErrors.WeakPassword : Result.Success();

    /// <param name="temporary">true quando um administrador define a senha por outra pessoa.</param>
    public void SetPasswordHash(string hash, bool temporary = false)
    {
        PasswordHash = hash;
        MustChangePassword = temporary;
        FailedLoginCount = 0;
        LockedUntil = null;
        Touch();
    }

    public Result Update(string name, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("User.Invalid", "Nome é obrigatório.");
        }

        if (!Enum.IsDefined(role))
        {
            return UserErrors.InvalidRole;
        }

        Name = name.Trim();
        Role = role;
        Touch();
        return Result.Success();
    }

    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    public bool IsLockedOut(DateTimeOffset now) => LockedUntil is { } until && until > now;

    public void RegisterLogin(DateTimeOffset now)
    {
        LastLoginAt = now;
        FailedLoginCount = 0;
        LockedUntil = null;
    }

    /// <summary>Senha errada: depois de <see cref="MaxFailedLogins"/> seguidas, bloqueia por <see cref="LockoutDuration"/>.</summary>
    public void RegisterFailedLogin(DateTimeOffset now)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= MaxFailedLogins)
        {
            LockedUntil = now + LockoutDuration;
            FailedLoginCount = 0;
        }
    }
}

public static class UserErrors
{
    public static readonly Error InvalidRole = Error.Validation("User.InvalidRole", "Perfil inválido.");
    public static readonly Error WeakPassword = Error.Validation("User.WeakPassword", $"A senha precisa ter ao menos {User.MinPasswordLength} caracteres.");
    public static readonly Error EmailAlreadyExists = Error.Conflict("User.EmailAlreadyExists", "Já existe um usuário com este e-mail.");
    public static readonly Error CannotChangeOwnAccess = Error.Validation("User.CannotChangeOwnAccess",
        "Você não pode mudar o próprio perfil nem se desativar. Peça a outro administrador.");
    public static readonly Error LastAdmin = Error.Validation("User.LastAdmin", "É preciso manter ao menos um administrador ativo.");
    public static readonly Error WrongCurrentPassword = Error.Validation("User.WrongCurrentPassword", "A senha atual não confere.");
    public static Error NotFound(Guid id) => Error.NotFound("User.NotFound", "Usuário não encontrado.");
}
