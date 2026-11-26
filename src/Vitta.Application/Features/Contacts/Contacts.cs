using FluentValidation;
using Mediator;
using Vitta.Application.Abstractions.Auth;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Clients;
using Vitta.Domain.Common;
using Vitta.Domain.Contacts;
using Vitta.Domain.Prospects;

namespace Vitta.Application.Features.Contacts;

// Historico de contatos do prospect e do cliente.

public sealed record ContactLogDto(Guid Id, DateTimeOffset OccurredAt, ContactChannel Channel, string Summary, string? UserName, Guid? UserId);

public interface IContactQueries
{
    Task<IReadOnlyList<ContactLogDto>> ListAsync(Guid? prospectId, Guid? clientId, CancellationToken ct = default);
}

/// <summary>Historico (mais recente primeiro). Prospect ja convertido mostra o historico do cliente.</summary>
public sealed record GetContactsQuery(Guid? ProspectId, Guid? ClientId) : IQuery<IReadOnlyList<ContactLogDto>>;

public sealed class GetContactsHandler(IContactQueries queries, IProspectRepository prospects) : IQueryHandler<GetContactsQuery, IReadOnlyList<ContactLogDto>>
{
    public async ValueTask<IReadOnlyList<ContactLogDto>> Handle(GetContactsQuery q, CancellationToken ct)
    {
        var clientId = q.ClientId;
        if (q.ProspectId is { } pid && clientId is null && await prospects.GetByIdAsync(pid, ct) is { ConvertedClientId: { } converted })
        {
            clientId = converted;
        }

        return await queries.ListAsync(q.ProspectId, clientId, ct);
    }
}

/// <param name="NextFollowUpOn">So para prospect: agenda o proximo contato.</param>
/// <param name="AdvanceStage">So para prospect: se estiver em "Novo", passa para "Contatado".</param>
public sealed record RegisterContactCommand(
    Guid? ProspectId, Guid? ClientId, ContactChannel Channel, string Summary, DateTimeOffset? OccurredAt,
    DateOnly? NextFollowUpOn, bool AdvanceStage) : ICommand<Result<Guid>>;

internal sealed class RegisterContactValidator : AbstractValidator<RegisterContactCommand>
{
    public RegisterContactValidator()
    {
        RuleFor(x => x.Summary).NotEmpty().MaximumLength(ContactLog.MaxSummary).WithName("O que foi tratado");
        RuleFor(x => x).Must(x => x.ProspectId is null != x.ClientId is null).WithMessage("Informe o prospect ou o cliente.");
    }
}

public sealed class RegisterContactHandler(
    IContactLogRepository logs, IProspectRepository prospects, IClientRepository clients, IUserRepository users,
    ICurrentUser currentUser, IUnitOfWork uow, TimeProvider time) : ICommandHandler<RegisterContactCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(RegisterContactCommand c, CancellationToken ct)
    {
        Guid? prospectId = null;
        var clientId = c.ClientId;
        if (c.ProspectId is { } pid)
        {
            var prospect = await prospects.GetByIdAsync(pid, ct);
            if (prospect is null)
            {
                return ProspectErrors.NotFound(pid);
            }

            if (prospect.ConvertedClientId is { } converted)
            {
                clientId = converted; // ja e cliente: o historico mora no cliente
            }
            else
            {
                prospectId = pid;
                var stage = c.AdvanceStage && prospect.Stage == ProspectStage.New ? ProspectStage.Contacted : prospect.Stage;
                if (stage != prospect.Stage || c.NextFollowUpOn is not null)
                {
                    prospect.MoveTo(stage, c.NextFollowUpOn ?? prospect.NextFollowUpOn);
                }
            }
        }
        else if (clientId is { } cid && await clients.GetByIdAsync(cid, ct) is null)
        {
            return ClientErrors.NotFound(cid);
        }

        var userId = currentUser.UserId;
        var userName = userId is { } uid ? (await users.GetByIdAsync(uid, ct))?.Name : null;
        var log = ContactLog.Create(prospectId, prospectId is null ? clientId : null, c.OccurredAt ?? time.GetUtcNow(), c.Channel, c.Summary, userId, userName);
        if (log.IsFailure)
        {
            return log.Error;
        }

        logs.Add(log.Value);
        await uow.SaveChangesAsync(ct);
        return log.Value.Id;
    }
}

public sealed record DeleteContactCommand(Guid Id) : ICommand<Result>;

public sealed class DeleteContactHandler(IContactLogRepository logs, IUnitOfWork uow) : ICommandHandler<DeleteContactCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteContactCommand c, CancellationToken ct)
    {
        var log = await logs.GetByIdAsync(c.Id, ct);
        if (log is null)
        {
            return ContactLog.NotFound;
        }

        logs.Remove(log);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
