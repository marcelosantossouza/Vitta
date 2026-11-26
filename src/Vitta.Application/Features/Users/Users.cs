using FluentValidation;
using Mediator;
using Vitta.Application.Abstractions.Auth;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Common;
using Vitta.Domain.Users;

namespace Vitta.Application.Features.Users;

// Gestao de usuarios: so administradores (a API exige o perfil nas rotas /users).

public sealed record UserListItemDto(
    Guid Id, string Name, string Email, UserRole Role, bool IsActive, bool MustChangePassword, DateTimeOffset? LastLoginAt, bool IsMe);

// ---------- Listar ----------

public sealed record GetUsersQuery : IQuery<IReadOnlyList<UserListItemDto>>;

public sealed class GetUsersHandler(IUserRepository users, ICurrentUser current) : IQueryHandler<GetUsersQuery, IReadOnlyList<UserListItemDto>>
{
    public async ValueTask<IReadOnlyList<UserListItemDto>> Handle(GetUsersQuery q, CancellationToken ct) =>
        (await users.ListAsync(ct))
            .Select(u => new UserListItemDto(u.Id, u.Name, u.Email, u.Role, u.IsActive, u.MustChangePassword, u.LastLoginAt, u.Id == current.UserId))
            .ToList();
}

// ---------- Criar ----------

/// <param name="TemporaryPassword">Senha provisoria definida pelo administrador; a pessoa troca no primeiro acesso.</param>
public sealed record CreateUserCommand(string Name, string Email, UserRole Role, string TemporaryPassword) : ICommand<Result<Guid>>;

internal sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150).WithName("Nome");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200).WithName("E-mail");
        RuleFor(x => x.Role).IsInEnum().WithName("Perfil");
    }
}

public sealed class CreateUserHandler(IUserRepository users, IPasswordHasher hasher, IUnitOfWork uow) : ICommandHandler<CreateUserCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(CreateUserCommand c, CancellationToken ct)
    {
        var password = User.ValidatePassword(c.TemporaryPassword);
        if (password.IsFailure)
        {
            return password.Error;
        }

        var created = User.Create(c.Name, c.Email, c.Role);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var user = created.Value;
        if (await users.GetByEmailAsync(user.Email, ct) is not null)
        {
            return UserErrors.EmailAlreadyExists;
        }

        user.SetPasswordHash(hasher.Hash(user, c.TemporaryPassword), temporary: true);
        users.Add(user);
        await uow.SaveChangesAsync(ct);
        return user.Id;
    }
}

// ---------- Editar nome / perfil ----------

public sealed record UpdateUserCommand(Guid Id, string Name, UserRole Role) : ICommand<Result>;

public sealed class UpdateUserHandler(IUserRepository users, ICurrentUser current, IUnitOfWork uow) : ICommandHandler<UpdateUserCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateUserCommand c, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(c.Id, ct);
        if (user is null)
        {
            return UserErrors.NotFound(c.Id);
        }

        if (user.Id == current.UserId && c.Role != user.Role)
        {
            return UserErrors.CannotChangeOwnAccess;
        }

        if (user is { Role: UserRole.Admin, IsActive: true } && c.Role != UserRole.Admin && await users.CountActiveAdminsAsync(ct) <= 1)
        {
            return UserErrors.LastAdmin;
        }

        var result = user.Update(c.Name, c.Role);
        if (result.IsFailure)
        {
            return result;
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---------- Ativar / desativar ----------

public sealed record SetUserActiveCommand(Guid Id, bool Active) : ICommand<Result>;

public sealed class SetUserActiveHandler(IUserRepository users, ICurrentUser current, IUnitOfWork uow) : ICommandHandler<SetUserActiveCommand, Result>
{
    public async ValueTask<Result> Handle(SetUserActiveCommand c, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(c.Id, ct);
        if (user is null)
        {
            return UserErrors.NotFound(c.Id);
        }

        if (!c.Active)
        {
            if (user.Id == current.UserId)
            {
                return UserErrors.CannotChangeOwnAccess;
            }

            if (user is { Role: UserRole.Admin, IsActive: true } && await users.CountActiveAdminsAsync(ct) <= 1)
            {
                return UserErrors.LastAdmin;
            }

            user.Deactivate();
        }
        else
        {
            user.Activate();
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---------- Redefinir senha (administrador define uma provisoria) ----------

public sealed record ResetUserPasswordCommand(Guid Id, string TemporaryPassword) : ICommand<Result>;

public sealed class ResetUserPasswordHandler(IUserRepository users, IPasswordHasher hasher, IUnitOfWork uow)
    : ICommandHandler<ResetUserPasswordCommand, Result>
{
    public async ValueTask<Result> Handle(ResetUserPasswordCommand c, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(c.Id, ct);
        if (user is null)
        {
            return UserErrors.NotFound(c.Id);
        }

        var password = User.ValidatePassword(c.TemporaryPassword);
        if (password.IsFailure)
        {
            return password;
        }

        // Tambem desbloqueia, caso a pessoa tenha errado a senha muitas vezes.
        user.SetPasswordHash(hasher.Hash(user, c.TemporaryPassword), temporary: true);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
