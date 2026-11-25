using FluentValidation;
using Mediator;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Abstractions.Queries;
using Vitta.Domain.Clients;
using Vitta.Domain.Common;
using Vitta.Domain.Insurers;
using Vitta.Domain.Policies;

namespace Vitta.Application.Features.Policies;

public sealed record PolicyDto(
    Guid Id, Guid ClientId, string ClientName, Guid InsurerId, string InsurerName, string PolicyNumber, string ProductName,
    decimal MonthlyPremium, DateOnly IssuedOn, PolicyStatus Status, DateOnly? EndedOn, string? EndReason,
    decimal? CommissionRate, decimal? MonthlyCommission);

/// <summary>Numeros da carteira cadastrada para os cards de Clientes e Apolices (so apolices ativas entram nos valores).</summary>
/// <param name="MonthlyCommission">Soma da comissao das apolices ativas que tem percentual.</param>
/// <param name="AverageCommissionRate">Comissao / premio das apolices com percentual (media ponderada pelo premio).</param>
/// <param name="ActiveWithoutRate">Apolices ativas sem percentual: enquanto houver, o total de comissao esta incompleto.</param>
public sealed record BookSummaryDto(
    int TotalClients, int ClientsWithActivePolicies, int ActivePolicies, int EndedPolicies,
    decimal MonthlyPremium, decimal MonthlyCommission, decimal? AverageCommissionRate, int ActiveWithoutRate);

public sealed record GetBookSummaryQuery : IQuery<BookSummaryDto>;

public sealed class GetBookSummaryHandler(IPolicyQueries queries) : IQueryHandler<GetBookSummaryQuery, BookSummaryDto>
{
    public async ValueTask<BookSummaryDto> Handle(GetBookSummaryQuery q, CancellationToken ct) => await queries.SummaryAsync(ct);
}

// ---- Listar ----
public sealed record GetPoliciesQuery(Guid? ClientId, Guid? InsurerId, PolicyStatus? Status, string? Search, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<PolicyDto>>;

public sealed class GetPoliciesHandler(IPolicyQueries queries) : IQueryHandler<GetPoliciesQuery, PagedResult<PolicyDto>>
{
    public async ValueTask<PagedResult<PolicyDto>> Handle(GetPoliciesQuery q, CancellationToken ct)
    {
        var (page, size) = Paging.Normalize(q.Page, q.PageSize);
        return await queries.SearchAsync(new PolicyFilter(q.ClientId, q.InsurerId, q.Status, q.Search), page, size, ct);
    }
}

// ---- Criar ----
public sealed record CreatePolicyCommand(
    Guid ClientId, Guid InsurerId, string PolicyNumber, string ProductName, decimal MonthlyPremium, DateOnly IssuedOn, decimal? CommissionRate = null)
    : ICommand<Result<Guid>>;

internal sealed class CreatePolicyValidator : AbstractValidator<CreatePolicyCommand>
{
    public CreatePolicyValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty().WithName("Cliente");
        RuleFor(x => x.InsurerId).NotEmpty().WithName("Seguradora");
        RuleFor(x => x.PolicyNumber).NotEmpty().MaximumLength(50).WithName("Número da apólice");
        RuleFor(x => x.ProductName).NotEmpty().MaximumLength(150).WithName("Produto");
        RuleFor(x => x.MonthlyPremium).GreaterThanOrEqualTo(0).WithName("Prêmio mensal");
    }
}

public sealed class CreatePolicyHandler(IPolicyRepository policies, IClientRepository clients, IInsurerRepository insurers, IUnitOfWork uow)
    : ICommandHandler<CreatePolicyCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(CreatePolicyCommand c, CancellationToken ct)
    {
        if (await clients.GetByIdAsync(c.ClientId, ct) is null)
        {
            return ClientErrors.NotFound(c.ClientId);
        }

        if (await insurers.GetByIdAsync(c.InsurerId, ct) is null)
        {
            return Insurer.NotFound(c.InsurerId);
        }

        if (await policies.NumberExistsAsync(c.InsurerId, c.PolicyNumber.Trim(), ct))
        {
            return PolicyErrors.NumberAlreadyExists;
        }

        var result = Policy.Create(c.ClientId, c.InsurerId, c.PolicyNumber, c.ProductName, c.MonthlyPremium, c.IssuedOn, c.CommissionRate);
        if (result.IsFailure)
        {
            return result.Error;
        }

        policies.Add(result.Value);
        await uow.SaveChangesAsync(ct);
        return result.Value.Id;
    }
}

// ---- Atualizar ----
public sealed record UpdatePolicyCommand(Guid Id, string ProductName, decimal MonthlyPremium, decimal? CommissionRate = null) : ICommand<Result>;

internal sealed class UpdatePolicyValidator : AbstractValidator<UpdatePolicyCommand>
{
    public UpdatePolicyValidator()
    {
        RuleFor(x => x.ProductName).NotEmpty().MaximumLength(150).WithName("Produto");
        RuleFor(x => x.MonthlyPremium).GreaterThanOrEqualTo(0).WithName("Prêmio mensal");
    }
}

public sealed class UpdatePolicyHandler(IPolicyRepository policies, IUnitOfWork uow) : ICommandHandler<UpdatePolicyCommand, Result>
{
    public async ValueTask<Result> Handle(UpdatePolicyCommand c, CancellationToken ct) =>
        await policies.GetByIdAsync(c.Id, ct) is { } policy
            ? await SaveIfOk(policy.Update(c.ProductName, c.MonthlyPremium, c.CommissionRate), uow, ct)
            : PolicyErrors.NotFound(c.Id);

    internal static async Task<Result> SaveIfOk(Result result, IUnitOfWork uow, CancellationToken ct)
    {
        if (result.IsSuccess)
        {
            await uow.SaveChangesAsync(ct);
        }

        return result;
    }
}

// ---- Encerrar / reativar ----
public sealed record EndPolicyCommand(Guid Id, PolicyStatus Status, DateOnly EndedOn, string? Reason) : ICommand<Result>;

internal sealed class EndPolicyValidator : AbstractValidator<EndPolicyCommand>
{
    public EndPolicyValidator()
    {
        RuleFor(x => x.Status).IsInEnum().NotEqual(PolicyStatus.Active).WithName("Status");
        RuleFor(x => x.Reason).MaximumLength(500).WithName("Motivo");
    }
}

public sealed class EndPolicyHandler(IPolicyRepository policies, IUnitOfWork uow) : ICommandHandler<EndPolicyCommand, Result>
{
    public async ValueTask<Result> Handle(EndPolicyCommand c, CancellationToken ct) =>
        await policies.GetByIdAsync(c.Id, ct) is { } policy
            ? await UpdatePolicyHandler.SaveIfOk(policy.End(c.Status, c.EndedOn, c.Reason), uow, ct)
            : PolicyErrors.NotFound(c.Id);
}

public sealed record ReactivatePolicyCommand(Guid Id) : ICommand<Result>;

public sealed class ReactivatePolicyHandler(IPolicyRepository policies, IUnitOfWork uow) : ICommandHandler<ReactivatePolicyCommand, Result>
{
    public async ValueTask<Result> Handle(ReactivatePolicyCommand c, CancellationToken ct)
    {
        var policy = await policies.GetByIdAsync(c.Id, ct);
        if (policy is null)
        {
            return PolicyErrors.NotFound(c.Id);
        }

        policy.Reactivate();
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
