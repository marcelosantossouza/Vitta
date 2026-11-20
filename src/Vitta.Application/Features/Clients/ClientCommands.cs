using FluentValidation;
using Mediator;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Abstractions.Queries;
using Vitta.Domain.Clients;
using Vitta.Domain.Common;

namespace Vitta.Application.Features.Clients;

// ---- Listar / detalhar ----
public sealed record GetClientsQuery(string? Search, int Page = 1, int PageSize = 20) : IQuery<PagedResult<ClientListItemDto>>;

public sealed class GetClientsHandler(IClientQueries queries) : IQueryHandler<GetClientsQuery, PagedResult<ClientListItemDto>>
{
    public async ValueTask<PagedResult<ClientListItemDto>> Handle(GetClientsQuery query, CancellationToken ct)
    {
        var (page, size) = Paging.Normalize(query.Page, query.PageSize);
        return await queries.SearchAsync(query.Search, page, size, ct);
    }
}

public sealed record GetClientByIdQuery(Guid Id) : IQuery<Result<ClientDetailDto>>;

public sealed class GetClientByIdHandler(IClientQueries queries) : IQueryHandler<GetClientByIdQuery, Result<ClientDetailDto>>
{
    public async ValueTask<Result<ClientDetailDto>> Handle(GetClientByIdQuery query, CancellationToken ct) =>
        await queries.GetDetailAsync(query.Id, ct) is { } dto ? dto : ClientErrors.NotFound(query.Id);
}

// ---- Criar ----
public sealed record CreateClientCommand(string FullName, string Cpf, DateOnly? BirthDate, string? Email, string? Phone, string? Notes = null)
    : ICommand<Result<Guid>>;

internal sealed class CreateClientValidator : AbstractValidator<CreateClientCommand>
{
    public CreateClientValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200).WithName("Nome");
        RuleFor(x => x.Cpf).NotEmpty().WithName("CPF");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithName("E-mail");
        RuleFor(x => x.Phone).MaximumLength(30).WithName("Telefone");
        RuleFor(x => x.Notes).MaximumLength(2000).WithName("Observações");
    }
}

public sealed class CreateClientHandler(IClientRepository clients, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateClientCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(CreateClientCommand command, CancellationToken ct)
    {
        var result = Client.Create(command.FullName, command.Cpf, command.BirthDate, command.Email, command.Phone, command.Notes);
        if (result.IsFailure)
        {
            return result.Error;
        }

        if (await clients.ExistsByCpfAsync(result.Value.Cpf, ct))
        {
            return ClientErrors.CpfAlreadyExists;
        }

        clients.Add(result.Value);
        await unitOfWork.SaveChangesAsync(ct);
        return result.Value.Id;
    }
}

// ---- Atualizar ----
public sealed record UpdateClientCommand(Guid Id, string FullName, DateOnly? BirthDate, string? Email, string? Phone, string? Notes)
    : ICommand<Result>;

internal sealed class UpdateClientValidator : AbstractValidator<UpdateClientCommand>
{
    public UpdateClientValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200).WithName("Nome");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithName("E-mail");
        RuleFor(x => x.Phone).MaximumLength(30).WithName("Telefone");
        RuleFor(x => x.Notes).MaximumLength(2000).WithName("Observações");
    }
}

public sealed class UpdateClientHandler(IClientRepository clients, IUnitOfWork uow) : ICommandHandler<UpdateClientCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateClientCommand command, CancellationToken ct)
    {
        var client = await clients.GetByIdAsync(command.Id, ct);
        if (client is null)
        {
            return ClientErrors.NotFound(command.Id);
        }

        var result = client.Update(command.FullName, command.BirthDate, command.Email, command.Phone, command.Notes);
        if (result.IsSuccess)
        {
            await uow.SaveChangesAsync(ct);
        }

        return result;
    }
}

// ---- Excluir ----
public sealed record DeleteClientCommand(Guid Id) : ICommand<Result>;

public sealed class DeleteClientHandler(IClientRepository clients, IUnitOfWork uow) : ICommandHandler<DeleteClientCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteClientCommand command, CancellationToken ct)
    {
        var client = await clients.GetByIdAsync(command.Id, ct);
        if (client is null)
        {
            return ClientErrors.NotFound(command.Id);
        }

        if (await clients.HasPoliciesAsync(client.Id, ct))
        {
            return ClientErrors.HasPolicies;
        }

        clients.Remove(client);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
