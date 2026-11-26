using Mediator;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Common;
using Vitta.Domain.Common;
using Vitta.Domain.Policies;
using Vitta.Domain.Referrals;

namespace Vitta.Application.Features.Referrals;

// Grupos de indicacao: o dono da corretora carimba segurados com uma pessoa/empresa/sindicato/codigo e acompanha
// o resultado mensal (com um percentual de nome livre). Sem acesso do parceiro por enquanto: o relatorio vira PDF.

// ---------- DTOs ----------

public sealed record ReferralGroupDto(
    Guid Id, string Name, string? Code, string? Notes, decimal? SharePercent, string ShareLabel,
    int Members, int ActiveMembers, decimal MonthlyPremium, decimal? MonthlyCommission, decimal? MonthlyShare);

/// <param name="Status">Situacao no fim do mes: Ativa, Nova (emitida no mes) ou Cancelada (encerrada no mes).</param>
public sealed record ReferralPolicyLineDto(
    Guid Id, string InsurerName, string PolicyNumber, string ProductName, decimal MonthlyPremium, decimal? CommissionRate,
    decimal? MonthlyCommission, DateOnly IssuedOn, string Status, DateOnly? EndedOn);

/// <param name="Status">Ativo (tem apolice valendo no fim do mes), Cancelado (perdeu no mes) ou Sem apólice.</param>
/// <param name="ImportedCommission">Comissao das planilhas das seguradoras no mes (pelo CPF); nulo se nao houver.</param>
public sealed record ReferralMemberDto(
    Guid ClientId, string Name, string Status, DateOnly? Since, decimal MonthlyPremium, decimal? MonthlyCommission,
    decimal? ImportedCommission, IReadOnlyList<ReferralPolicyLineDto> Policies);

public sealed record ReferralMonthDto(DateOnly Month, int ActiveMembers, decimal Premium, decimal? Commission, decimal? Share);

/// <param name="CommissionBase">"Planilhas" quando ha comissao importada das seguradoras no mes; senao "Apólices" (premio x %).</param>
public sealed record ReferralReportDto(
    ReferralGroupDto Group, DateOnly Month, int ActiveMembers, int NewPolicies, int CancelledPolicies,
    decimal Premium, decimal? EstimatedCommission, int PoliciesWithoutRate, decimal? ImportedCommission,
    string CommissionBase, decimal? Commission, decimal? Share,
    IReadOnlyList<ReferralMemberDto> Members, IReadOnlyList<ReferralMonthDto> History);

// ---------- Dados crus (carregados pela persistencia) ----------

public sealed record ReferralMemberData(Guid ClientId, string Name, string Cpf, Guid GroupId);

public sealed record ReferralPolicyData(
    Guid Id, Guid ClientId, string InsurerName, string PolicyNumber, string ProductName, decimal MonthlyPremium,
    decimal? CommissionRate, DateOnly IssuedOn, PolicyStatus Status, DateOnly? EndedOn);

/// <param name="Cpf">So digitos.</param>
public sealed record ImportedCommissionData(string Cpf, DateOnly Month, decimal Amount);

public sealed record ReferralData(
    IReadOnlyList<ReferralGroup> Groups, IReadOnlyList<ReferralMemberData> Members,
    IReadOnlyList<ReferralPolicyData> Policies, IReadOnlyList<ImportedCommissionData> ImportedCommissions);

public interface IReferralQueries
{
    /// <summary>Grupos, membros, apolices dos membros e comissao importada (todos os grupos, ou so um).</summary>
    Task<ReferralData> LoadAsync(Guid? groupId, CancellationToken ct = default);
}

// ---------- Calculo (puro, testavel) ----------

public static class ReferralReport
{
    private static DateOnly End(DateOnly month) => month.AddMonths(1).AddDays(-1);

    /// <summary>Valendo no fim do mes: emitida ate o fim do mes e (ativa ou encerrada depois dele).</summary>
    public static bool InForce(ReferralPolicyData p, DateOnly month) =>
        p.IssuedOn <= End(month) && (p.Status == PolicyStatus.Active || p.EndedOn > End(month));

    private static bool EndedIn(ReferralPolicyData p, DateOnly month) =>
        p.Status != PolicyStatus.Active && p.EndedOn is { } e && e >= month && e <= End(month);

    private static decimal? SumOrNull(IEnumerable<decimal?> values)
    {
        var list = values.Where(v => v is not null).ToList();
        return list.Count > 0 ? list.Sum() : null;
    }

    public static ReferralReportDto Build(ReferralGroup group, ReferralData data, DateOnly month)
    {
        month = new DateOnly(month.Year, month.Month, 1);
        var members = data.Members.Where(m => m.GroupId == group.Id).ToList();
        var ids = members.Select(m => m.ClientId).ToHashSet();
        var policies = data.Policies.Where(p => ids.Contains(p.ClientId)).ToList();
        var byClient = policies.ToLookup(p => p.ClientId);

        var rows = members.Select(m =>
        {
            var mine = byClient[m.ClientId].ToList();
            var inForce = mine.Where(p => InForce(p, month)).ToList();
            var lines = mine.Where(p => InForce(p, month) || EndedIn(p, month))
                .OrderBy(p => p.IssuedOn)
                .Select(p => new ReferralPolicyLineDto(p.Id, p.InsurerName, p.PolicyNumber, p.ProductName, p.MonthlyPremium, p.CommissionRate,
                    InForce(p, month) ? Policy.CommissionOf(p.MonthlyPremium, p.CommissionRate) : null, p.IssuedOn,
                    EndedIn(p, month) ? "Cancelada" : p.IssuedOn >= month ? "Nova" : "Ativa", p.EndedOn))
                .ToList();
            var status = inForce.Count > 0 ? "Ativo" : lines.Count > 0 ? "Cancelado" : "Sem apólice";
            var imported = SumOrNull(data.ImportedCommissions.Where(c => c.Cpf == m.Cpf && c.Month == month).Select(c => (decimal?)c.Amount));
            return new ReferralMemberDto(m.ClientId, m.Name, status, inForce.Count > 0 ? inForce.Min(p => p.IssuedOn) : null,
                inForce.Sum(p => p.MonthlyPremium), SumOrNull(inForce.Select(p => Policy.CommissionOf(p.MonthlyPremium, p.CommissionRate))),
                imported, lines);
        })
        .OrderBy(r => r.Status == "Ativo" ? 0 : r.Status == "Cancelado" ? 1 : 2)
        .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

        var (commissionBase, commission) = CommissionFor(members, policies, data.ImportedCommissions, month);
        var history = Enumerable.Range(0, 12).Select(i => month.AddMonths(-11 + i)).Select(m =>
        {
            var (_, c) = CommissionFor(members, policies, data.ImportedCommissions, m);
            var active = members.Count(x => byClient[x.ClientId].Any(p => InForce(p, m)));
            return new ReferralMonthDto(m, active, policies.Where(p => InForce(p, m)).Sum(p => p.MonthlyPremium), c,
                ReferralGroup.ShareOf(c, group.SharePercent));
        }).ToList();

        var inForceNow = policies.Where(p => InForce(p, month)).ToList();
        return new ReferralReportDto(
            Summary(group, data, month), month,
            ActiveMembers: rows.Count(r => r.Status == "Ativo"),
            NewPolicies: policies.Count(p => p.IssuedOn >= month && p.IssuedOn <= End(month)),
            CancelledPolicies: policies.Count(p => EndedIn(p, month)),
            Premium: inForceNow.Sum(p => p.MonthlyPremium),
            EstimatedCommission: SumOrNull(inForceNow.Select(p => Policy.CommissionOf(p.MonthlyPremium, p.CommissionRate))),
            PoliciesWithoutRate: inForceNow.Count(p => p.CommissionRate is null),
            ImportedCommission: SumOrNull(rows.Select(r => r.ImportedCommission)),
            CommissionBase: commissionBase,
            Commission: commission,
            Share: ReferralGroup.ShareOf(commission, group.SharePercent),
            Members: rows,
            History: history);
    }

    /// <summary>Comissao do mes: a das planilhas das seguradoras se houver; senao a estimada pelas apolices (premio x %).</summary>
    private static (string Base, decimal? Commission) CommissionFor(
        IReadOnlyList<ReferralMemberData> members, IReadOnlyList<ReferralPolicyData> policies,
        IReadOnlyList<ImportedCommissionData> imported, DateOnly month)
    {
        var cpfs = members.Select(m => m.Cpf).ToHashSet();
        var fromSheets = SumOrNull(imported.Where(c => c.Month == month && cpfs.Contains(c.Cpf)).Select(c => (decimal?)c.Amount));
        if (fromSheets is not null)
        {
            return ("Planilhas", fromSheets);
        }

        return ("Apólices", SumOrNull(policies.Where(p => InForce(p, month)).Select(p => Policy.CommissionOf(p.MonthlyPremium, p.CommissionRate))));
    }

    /// <summary>Resumo do grupo no mes (estimado pelas apolices) para a lista.</summary>
    public static ReferralGroupDto Summary(ReferralGroup g, ReferralData data, DateOnly month)
    {
        var members = data.Members.Where(m => m.GroupId == g.Id).ToList();
        var ids = members.Select(m => m.ClientId).ToHashSet();
        var inForce = data.Policies.Where(p => ids.Contains(p.ClientId) && InForce(p, month)).ToList();
        var commission = SumOrNull(inForce.Select(p => Policy.CommissionOf(p.MonthlyPremium, p.CommissionRate)));
        return new ReferralGroupDto(g.Id, g.Name, g.Code, g.Notes, g.SharePercent, g.ShareLabel,
            members.Count, inForce.Select(p => p.ClientId).Distinct().Count(), inForce.Sum(p => p.MonthlyPremium), commission,
            ReferralGroup.ShareOf(commission, g.SharePercent));
    }
}

// ---------- Consultas ----------

public sealed record GetReferralGroupsQuery : IQuery<IReadOnlyList<ReferralGroupDto>>;

public sealed class GetReferralGroupsHandler(IReferralQueries queries, TimeProvider time) : IQueryHandler<GetReferralGroupsQuery, IReadOnlyList<ReferralGroupDto>>
{
    public async ValueTask<IReadOnlyList<ReferralGroupDto>> Handle(GetReferralGroupsQuery q, CancellationToken ct)
    {
        var data = await queries.LoadAsync(null, ct);
        var today = DateOnly.FromDateTime(time.LocalNow());
        var month = new DateOnly(today.Year, today.Month, 1);
        return data.Groups.Select(g => ReferralReport.Summary(g, data, month))
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}

/// <param name="Month">Qualquer dia do mes desejado; nulo = mes atual.</param>
public sealed record GetReferralReportQuery(Guid Id, DateOnly? Month) : IQuery<Result<ReferralReportDto>>;

public sealed class GetReferralReportHandler(IReferralQueries queries, TimeProvider time) : IQueryHandler<GetReferralReportQuery, Result<ReferralReportDto>>
{
    public async ValueTask<Result<ReferralReportDto>> Handle(GetReferralReportQuery q, CancellationToken ct)
    {
        var data = await queries.LoadAsync(q.Id, ct);
        var group = data.Groups.FirstOrDefault(g => g.Id == q.Id);
        if (group is null)
        {
            return ReferralErrors.NotFound;
        }

        return ReferralReport.Build(group, data, q.Month ?? DateOnly.FromDateTime(time.LocalNow()));
    }
}

// ---------- Comandos ----------

public sealed record SaveReferralGroupCommand(Guid? Id, string Name, string? Code, string? Notes, decimal? SharePercent, string? ShareLabel)
    : ICommand<Result<Guid>>;

public sealed class SaveReferralGroupHandler(IReferralGroupRepository groups, IUnitOfWork uow) : ICommandHandler<SaveReferralGroupCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(SaveReferralGroupCommand c, CancellationToken ct)
    {
        var code = string.IsNullOrWhiteSpace(c.Code) ? null : c.Code.Trim().ToUpperInvariant();
        if (code is not null && await groups.CodeExistsAsync(code, c.Id, ct))
        {
            return ReferralErrors.CodeAlreadyExists;
        }

        ReferralGroup group;
        if (c.Id is { } id)
        {
            var found = await groups.GetByIdAsync(id, ct);
            if (found is null)
            {
                return ReferralErrors.NotFound;
            }

            var updated = found.Update(c.Name, code, c.Notes, c.SharePercent, c.ShareLabel);
            if (updated.IsFailure)
            {
                return updated.Error;
            }

            group = found;
        }
        else
        {
            var created = ReferralGroup.Create(c.Name, code, c.Notes, c.SharePercent, c.ShareLabel);
            if (created.IsFailure)
            {
                return created.Error;
            }

            group = created.Value;
            groups.Add(group);
        }

        await uow.SaveChangesAsync(ct);
        return group.Id;
    }
}

/// <summary>Exclui o grupo; os segurados continuam na carteira, so perdem o carimbo.</summary>
public sealed record DeleteReferralGroupCommand(Guid Id) : ICommand<Result>;

public sealed class DeleteReferralGroupHandler(IReferralGroupRepository groups, IClientRepository clients, IUnitOfWork uow)
    : ICommandHandler<DeleteReferralGroupCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteReferralGroupCommand c, CancellationToken ct)
    {
        var group = await groups.GetByIdAsync(c.Id, ct);
        if (group is null)
        {
            return ReferralErrors.NotFound;
        }

        foreach (var client in await clients.ListByReferralAsync(c.Id, ct))
        {
            client.AssignReferral(null);
        }

        groups.Remove(group);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Carimba (ou tira o carimbo, com GroupId nulo) de varios segurados. Quem estava em outro grupo muda.</summary>
public sealed record AssignReferralCommand(Guid? GroupId, IReadOnlyList<Guid> ClientIds) : ICommand<Result<int>>;

public sealed class AssignReferralHandler(IReferralGroupRepository groups, IClientRepository clients, IUnitOfWork uow)
    : ICommandHandler<AssignReferralCommand, Result<int>>
{
    public async ValueTask<Result<int>> Handle(AssignReferralCommand c, CancellationToken ct)
    {
        if (c.GroupId is { } id && await groups.GetByIdAsync(id, ct) is null)
        {
            return ReferralErrors.NotFound;
        }

        var list = await clients.ListByIdsAsync(c.ClientIds.Distinct().Take(5000).ToList(), ct);
        foreach (var client in list)
        {
            client.AssignReferral(c.GroupId);
        }

        await uow.SaveChangesAsync(ct);
        return list.Count;
    }
}
