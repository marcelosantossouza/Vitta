using FluentValidation;
using Mediator;
using Vitta.Application.Abstractions.Auth;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Common;
using Vitta.Domain.Users;

namespace Vitta.Application.Features.Auth;

public sealed record UserDto(Guid Id, string Name, string Email)
{
    public static UserDto From(User u) => new(u.Id, u.Name, u.Email);
}

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);

public static class AuthErrors
{
    public static readonly Error InvalidCredentials = new("Auth.InvalidCredentials", "E-mail ou senha inválidos.", ErrorType.Unauthorized);
    public static readonly Error NotAuthenticated = new("Auth.NotAuthenticated", "Usuário não autenticado.", ErrorType.Unauthorized);
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

public sealed class LoginHandler(IUserRepository users, IPasswordHasher hasher, ITokenService tokens, IUnitOfWork uow)
    : ICommandHandler<LoginCommand, Result<LoginResponse>>
{
    public async ValueTask<Result<LoginResponse>> Handle(LoginCommand command, CancellationToken ct)
    {
        var user = await users.GetByEmailAsync(User.NormalizeEmail(command.Email), ct);
        if (user is null || !hasher.Verify(user, user.PasswordHash, command.Password))
        {
            return AuthErrors.InvalidCredentials;
        }

        user.RegisterLogin();
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
