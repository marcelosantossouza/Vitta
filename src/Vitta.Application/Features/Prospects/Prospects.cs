using FluentValidation;
using Mediator;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Abstractions.Queries;
using Vitta.Domain.Clients;
using Vitta.Domain.Common;
using Vitta.Domain.Prospects;

namespace Vitta.Application.Features.Prospects;

public sealed record ProspectDto(
    Guid Id, string Name, string? Phone, string? Email, string? Source, ProspectStage Stage, DateOnly? NextFollowUpOn,
    string? Notes, Guid? ConvertedClientId, DateTimeOffset CreatedAt)
{
    public static ProspectDto From(Prospect p) =>
        new(p.Id, p.Name, p.Phone, p.Email, p.Source, p.Stage, p.NextFollowUpOn, p.Notes, p.ConvertedClientId, p.CreatedAt);
}

internal static class ProspectRules
{
    public static void Apply<T>(AbstractValidator<T> v, Func<T, string> name, Func<T, string?> email, Func<T, string?> phone, Func<T, string?> notes)
    {
        v.RuleFor(x => name(x)).NotEmpty().MaximumLength(200).WithName("Nome");
        v.RuleFor(x => email(x)).EmailAddress().When(x => !string.IsNullOrWhiteSpace(email(x))).WithName("E-mail");
        v.RuleFor(x => phone(x)).MaximumLength(30).WithName("Telefone");
        v.RuleFor(x => notes(x)).MaximumLength(2000).WithName("Observações");
    }
}

// ---- Listar ----
public sealed record GetProspectsQuery(ProspectStage? Stage, string? Search) : IQuery<IReadOnlyList<ProspectDto>>;

public sealed class GetProspectsHandler(IProspectQueries queries) : IQueryHandler<GetProspectsQuery, IReadOnlyList<ProspectDto>>
{
    public async ValueTask<IReadOnlyList<ProspectDto>> Handle(GetProspectsQuery q, CancellationToken ct) =>
        await queries.ListAsync(q.Stage, q.Search, ct);
}

// ---- Criar ----
public sealed record CreateProspectCommand(string Name, string? Phone, string? Email, string? Source, string? Notes, DateOnly? NextFollowUpOn)
    : ICommand<Result<Guid>>;

internal sealed class CreateProspectValidator : AbstractValidator<CreateProspectCommand>
{
    public CreateProspectValidator() => ProspectRules.Apply(this, x => x.Name, x => x.Email, x => x.Phone, x => x.Notes);
}

public sealed class CreateProspectHandler(IProspectRepository prospects, IUnitOfWork uow) : ICommandHandler<CreateProspectCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(CreateProspectCommand c, CancellationToken ct)
    {
        var result = Prospect.Create(c.Name, c.Phone, c.Email, c.Source, c.Notes, c.NextFollowUpOn);
        if (result.IsFailure)
        {
            return result.Error;
        }

        prospects.Add(result.Value);
        await uow.SaveChangesAsync(ct);
        return result.Value.Id;
    }
}

// ---- Atualizar ----
public sealed record UpdateProspectCommand(Guid Id, string Name, string? Phone, string? Email, string? Source, string? Notes, DateOnly? NextFollowUpOn)
    : ICommand<Result>;

internal sealed class UpdateProspectValidator : AbstractValidator<UpdateProspectCommand>
{
    public UpdateProspectValidator() => ProspectRules.Apply(this, x => x.Name, x => x.Email, x => x.Phone, x => x.Notes);
}

public sealed class UpdateProspectHandler(IProspectRepository prospects, IUnitOfWork uow) : ICommandHandler<UpdateProspectCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateProspectCommand c, CancellationToken ct)
    {
        var prospect = await prospects.GetByIdAsync(c.Id, ct);
        if (prospect is null)
        {
            return ProspectErrors.NotFound(c.Id);
        }

        var result = prospect.Update(c.Name, c.Phone, c.Email, c.Source, c.Notes, c.NextFollowUpOn);
        if (result.IsSuccess)
        {
            await uow.SaveChangesAsync(ct);
        }

        return result;
    }
}

// ---- Mudar estagio ----
public sealed record MoveProspectStageCommand(Guid Id, ProspectStage Stage, DateOnly? NextFollowUpOn) : ICommand<Result>;

internal sealed class MoveProspectStageValidator : AbstractValidator<MoveProspectStageCommand>
{
    public MoveProspectStageValidator() => RuleFor(x => x.Stage).IsInEnum().WithName("Etapa");
}

public sealed class MoveProspectStageHandler(IProspectRepository prospects, IUnitOfWork uow) : ICommandHandler<MoveProspectStageCommand, Result>
{
    public async ValueTask<Result> Handle(MoveProspectStageCommand c, CancellationToken ct)
    {
        var prospect = await prospects.GetByIdAsync(c.Id, ct);
        if (prospect is null)
        {
            return ProspectErrors.NotFound(c.Id);
        }

        prospect.MoveTo(c.Stage, c.NextFollowUpOn);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---- Converter em cliente ----
public sealed record ConvertProspectCommand(Guid Id, string Cpf, DateOnly? BirthDate) : ICommand<Result<Guid>>;

internal sealed class ConvertProspectValidator : AbstractValidator<ConvertProspectCommand>
{
    public ConvertProspectValidator() => RuleFor(x => x.Cpf).NotEmpty().WithName("CPF");
}

public sealed class ConvertProspectHandler(IProspectRepository prospects, IClientRepository clients, IUnitOfWork uow)
    : ICommandHandler<ConvertProspectCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(ConvertProspectCommand c, CancellationToken ct)
    {
        var prospect = await prospects.GetByIdAsync(c.Id, ct);
        if (prospect is null)
        {
            return ProspectErrors.NotFound(c.Id);
        }

        var client = Client.Create(prospect.Name, c.Cpf, c.BirthDate, prospect.Email, prospect.Phone, prospect.Notes);
        if (client.IsFailure)
        {
            return client.Error;
        }

        if (await clients.ExistsByCpfAsync(client.Value.Cpf, ct))
        {
            return ClientErrors.CpfAlreadyExists;
        }

        var converted = prospect.MarkConverted(client.Value.Id);
        if (converted.IsFailure)
        {
            return converted.Error;
        }

        clients.Add(client.Value);
        await uow.SaveChangesAsync(ct);
        return client.Value.Id;
    }
}

// ---- Excluir ----
public sealed record DeleteProspectCommand(Guid Id) : ICommand<Result>;

public sealed class DeleteProspectHandler(IProspectRepository prospects, IUnitOfWork uow) : ICommandHandler<DeleteProspectCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteProspectCommand c, CancellationToken ct)
    {
        var prospect = await prospects.GetByIdAsync(c.Id, ct);
        if (prospect is null)
        {
            return ProspectErrors.NotFound(c.Id);
        }

        prospects.Remove(prospect);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
