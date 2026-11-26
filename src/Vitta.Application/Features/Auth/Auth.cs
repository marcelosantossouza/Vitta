using FluentValidation;
using Mediator;
using Vitta.Application.Abstractions.Auth;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Common;
using Vitta.Domain.Users;

namespace Vitta.Application.Features.Auth;

/// <param name="MustChangePassword">Senha provisoria: o front leva para a troca antes de liberar o sistema.</param>
public sealed record UserDto(Guid Id, string Name, string Email, UserRole Role, bool MustChangePassword)
{
    public static UserDto From(User u) => new(u.Id, u.Name, u.Email, u.Role, u.MustChangePassword);
}

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);

public static class AuthErrors
{
    public static readonly Error InvalidCredentials = new("Auth.InvalidCredentials", "E-mail ou senha inválidos.", ErrorType.Unauthorized);
    public static readonly Error NotAuthenticated = new("Auth.NotAuthenticated", "Usuário não autenticado.", ErrorType.Unauthorized);
    public static Error LockedOut(DateTimeOffset until) => new("Auth.LockedOut",
        $"Muitas tentativas com senha errada. Tente de novo em alguns minutos (liberado às {until.ToOffset(TimeSpan.FromHours(-3)):HH:mm}, horário de Brasília).",
        ErrorType.Unauthorized);
}

// ---- Login ----
public sealed record LoginCommand(string Email, string Password) : ICommand<Result<LoginResponse>>;

internal sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginHandler(IUserRepository users, IPasswordHasher hasher, ITokenService tokens, IUnitOfWork uow, TimeProvider time)
    : ICommandHandler<LoginCommand, Result<LoginResponse>>
{
    public async ValueTask<Result<LoginResponse>> Handle(LoginCommand command, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var user = await users.GetByEmailAsync(User.NormalizeEmail(command.Email), ct);
        // Usuario inexistente ou desativado: mesma mensagem de senha errada (nao revela quais e-mails existem).
        if (user is null || !user.IsActive)
        {
            return AuthErrors.InvalidCredentials;
        }

        if (user.IsLockedOut(now))
        {
            return AuthErrors.LockedOut(user.LockedUntil!.Value);
        }

        if (!hasher.Verify(user, user.PasswordHash, command.Password))
        {
            user.RegisterFailedLogin(now);
            await uow.SaveChangesAsync(ct);
            return user.IsLockedOut(now) ? AuthErrors.LockedOut(user.LockedUntil!.Value) : AuthErrors.InvalidCredentials;
        }

        user.RegisterLogin(now);
        await uow.SaveChangesAsync(ct);
        var token = tokens.Create(user);
        return new LoginResponse(token.Token, token.ExpiresAt, UserDto.From(user));
    }
}

// ---- Me ----
public sealed record GetCurrentUserQuery : IQuery<Result<UserDto>>;

public sealed class GetCurrentUserHandler(ICurrentUser current, IUserRepository users) : IQueryHandler<GetCurrentUserQuery, Result<UserDto>>
{
    public async ValueTask<Result<UserDto>> Handle(GetCurrentUserQuery query, CancellationToken ct)
    {
        var user = current.UserId is { } id ? await users.GetByIdAsync(id, ct) : null;
        return user is null ? AuthErrors.NotAuthenticated : UserDto.From(user);
    }
}

// ---- Trocar a propria senha ----
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand<Result>;

public sealed class ChangePasswordHandler(ICurrentUser current, IUserRepository users, IPasswordHasher hasher, IUnitOfWork uow)
    : ICommandHandler<ChangePasswordCommand, Result>
{
    public async ValueTask<Result> Handle(ChangePasswordCommand c, CancellationToken ct)
    {
        var user = current.UserId is { } id ? await users.GetByIdAsync(id, ct) : null;
        if (user is null)
        {
            return AuthErrors.NotAuthenticated;
        }

        if (!hasher.Verify(user, user.PasswordHash, c.CurrentPassword ?? string.Empty))
        {
            return UserErrors.WrongCurrentPassword;
        }

        var valid = User.ValidatePassword(c.NewPassword);
        if (valid.IsFailure)
        {
            return valid;
        }

        if (c.NewPassword == c.CurrentPassword)
        {
            return Error.Validation("User.SamePassword", "A nova senha precisa ser diferente da atual.");
        }

        user.SetPasswordHash(hasher.Hash(user, c.NewPassword));
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
