using Microsoft.EntityFrameworkCore;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Application.Abstractions.Queries;
using Vitta.Application.Features.Clients;
using Vitta.Application.Features.Dashboard;
using Vitta.Application.Features.Imports;
using Vitta.Application.Features.Policies;
using Vitta.Application.Features.Portfolio;
using Vitta.Application.Features.Prospects;
using Vitta.Domain.Common;
using Vitta.Domain.Imports;
using Vitta.Domain.Policies;
using Vitta.Domain.Portfolio;
using Vitta.Domain.Prospects;
using Vitta.Persistence.Context;

namespace Vitta.Persistence.Queries;

// Observacao: somas de dinheiro sao feitas em memoria sobre listas pequenas, porque o SQLite
// (banco de dev) nao agrega decimal no SQL. Para o porte de uma corretora isso e irrelevante.

internal sealed class ClientQueries(VittaDbContext db) : IClientQueries
{
    public async Task<PagedResult<ClientListItemDto>> SearchAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Clients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            // SearchName ja vem normalizado do dominio: busca sem acento/maiuscula em qualquer banco.
            var name = TextNormalizer.ForSearch(search);
            var digits = new string(search.Where(char.IsDigit).ToArray());
            query = digits.Length > 0
                ? query.Where(c => c.SearchName.Contains(name) || ((string)(object)c.Cpf).Contains(digits))
                : query.Where(c => c.SearchName.Contains(name));
        }

        var total = await query.CountAsync(ct);
        var clients = await query.OrderBy(c => c.SearchName).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new { c.Id, c.FullName, c.Cpf, c.Phone, c.Email })
            .ToListAsync(ct);

        var ids = clients.Select(c => c.Id).ToList();
        var policies = await db.Policies.AsNoTracking()
            .Where(p => ids.Contains(p.ClientId) && p.Status == PolicyStatus.Active)
            .Select(p => new { p.ClientId, p.MonthlyPremium, p.CommissionRate })
            .ToListAsync(ct);
        var byClient = policies.GroupBy(p => p.ClientId).ToDictionary(g => g.Key, g => (
            Count: g.Count(),
            Sum: g.Sum(x => x.MonthlyPremium),
            Commission: g.Any(x => x.CommissionRate is not null) ? g.Sum(x => Policy.CommissionOf(x.MonthlyPremium, x.CommissionRate) ?? 0) : (decimal?)null,
            WithoutRate: g.Count(x => x.CommissionRate is null)));

        var items = clients.Select(c =>
        {
            var (count, sum, commission, withoutRate) = byClient.GetValueOrDefault(c.Id);
            return new ClientListItemDto(c.Id, c.FullName, c.Cpf.Formatted, c.Phone, c.Email, count, sum, commission, withoutRate);
        }).ToList();

        return new PagedResult<ClientListItemDto>(items, page, pageSize, total);
    }

    public async Task<ClientDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var c = await db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null)
        {
            return null;
        }

        var policies = await PolicyQueries.Project(db, db.Policies.AsNoTracking().Where(p => p.ClientId == id))
            .ToListAsync(ct);

        return new ClientDetailDto(c.Id, c.FullName, c.Cpf.Formatted, c.BirthDate, c.Email, c.Phone, c.Notes, c.CreatedAt,
            policies.OrderBy(p => p.Status).ThenByDescending(p => p.IssuedOn).ToList());
    }
}

internal sealed class PolicyQueries(VittaDbContext db) : IPolicyQueries
{
    public async Task<PagedResult<PolicyDto>> SearchAsync(PolicyFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Policies.AsNoTracking();
        if (filter.ClientId is { } clientId)
        {
            query = query.Where(p => p.ClientId == clientId);
        }

        if (filter.InsurerId is { } insurerId)
        {
            query = query.Where(p => p.InsurerId == insurerId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            var name = TextNormalizer.ForSearch(term);
            var clientIds = db.Clients.Where(c => c.SearchName.Contains(name)).Select(c => c.Id);
            query = query.Where(p => p.PolicyNumber.Contains(term) || clientIds.Contains(p.ClientId));
        }

        var total = await query.CountAsync(ct);
        var items = await Project(db, query.OrderByDescending(p => p.IssuedOn).ThenBy(p => p.PolicyNumber).Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(ct);
        return new PagedResult<PolicyDto>(items, page, pageSize, total);
    }

    public async Task<BookSummaryDto> SummaryAsync(CancellationToken ct = default)
    {
        var totalClients = await db.Clients.CountAsync(ct);
        var policies = await db.Policies.AsNoTracking()
            .Select(p => new { p.ClientId, p.Status, p.MonthlyPremium, p.CommissionRate })
            .ToListAsync(ct);
        var active = policies.Where(p => p.Status == PolicyStatus.Active).ToList();
        var withRate = active.Where(p => p.CommissionRate is not null).ToList();
        var commission = withRate.Sum(p => Policy.CommissionOf(p.MonthlyPremium, p.CommissionRate) ?? 0);
        var premiumWithRate = withRate.Sum(p => p.MonthlyPremium);

        return new BookSummaryDto(
            totalClients,
            active.Select(p => p.ClientId).Distinct().Count(),
            active.Count,
            policies.Count - active.Count,
            active.Sum(p => p.MonthlyPremium),
            commission,
            premiumWithRate > 0 ? Math.Round(commission / premiumWithRate * 100m, 2) : null,
            active.Count - withRate.Count);
    }

    internal static IQueryable<PolicyDto> Project(VittaDbContext db, IQueryable<Policy> policies) =>
        from p in policies
        join c in db.Clients on p.ClientId equals c.Id
        join i in db.Insurers on p.InsurerId equals i.Id
        select new PolicyDto(p.Id, p.ClientId, c.FullName, p.InsurerId, i.Name, p.PolicyNumber, p.ProductName, p.MonthlyPremium,
            p.IssuedOn, p.Status, p.EndedOn, p.EndReason, p.CommissionRate, Policy.CommissionOf(p.MonthlyPremium, p.CommissionRate));
}

internal sealed class ProspectQueries(VittaDbContext db) : IProspectQueries
{
    public async Task<IReadOnlyList<ProspectDto>> ListAsync(ProspectStage? stage, string? search, CancellationToken ct = default)
    {
        var query = db.Prospects.AsNoTracking();
        if (stage is { } s)
        {
            query = query.Where(p => p.Stage == s);
        }

        var list = await query.ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = TextNormalizer.ForSearch(search);
            list = list.Where(p => TextNormalizer.ForSearch(p.Name).Contains(term) || (p.Phone ?? "").Contains(search.Trim())).ToList();
        }

        return list
            .OrderBy(p => p.NextFollowUpOn ?? DateOnly.MaxValue)
            .ThenByDescending(p => p.CreatedAt)
            .Select(ProspectDto.From)
            .ToList();
    }
}

internal sealed class PortfolioQueries(VittaDbContext db) : IPortfolioQueries
{
    public async Task<IReadOnlyList<PortfolioImportDto>> ListImportsAsync(CancellationToken ct = default)
    {
        var imports = await db.PortfolioImports.AsNoTracking().Include(i => i.Changes).ToListAsync(ct);
        var insurers = await InsurerNames(ct);
        return imports
            .OrderByDescending(i => i.ReferenceMonth).ThenBy(i => insurers.GetValueOrDefault(i.InsurerId))
            .Select(i => ToDto(i, insurers))
            .ToList();
    }

    public async Task<PortfolioImportDetailDto?> GetImportAsync(Guid id, CancellationToken ct = default)
    {
        var i = await db.PortfolioImports.AsNoTracking().Include(x => x.Changes).Include(x => x.Rows).AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (i is null)
        {
            return null;
        }

        var previous = await db.PortfolioImports.AsNoTracking().Include(x => x.Rows)
            .Where(x => x.InsurerId == i.InsurerId && x.ReferenceMonth < i.ReferenceMonth)
            .OrderByDescending(x => x.ReferenceMonth)
            .FirstOrDefaultAsync(ct);
        var totals = new ImportTotalsDto(
            MonthTotalsDto.From(i.ReferenceMonth, i.Rows.Select(r => r.ToRow()).ToList()),
            previous is null ? null : MonthTotalsDto.From(previous.ReferenceMonth, previous.Rows.Select(r => r.ToRow()).ToList()));

        var insurers = await InsurerNames(ct);
        var dto = ToDto(i, insurers);
        var clientsByCpf = await ClientIdsByCpf(i.Changes.Select(c => c.HolderCpf), ct);
        var changes = i.Changes
            .OrderBy(c => c.Type == PortfolioChangeType.Left ? 0 : c.Type == PortfolioChangeType.Entered ? 1 : 2)
            .ThenBy(c => c.HolderName)
            .Select(c => ToChangeDto(c, i, dto.InsurerName, clientsByCpf))
            .ToList();

        return new PortfolioImportDetailDto(dto.Id, dto.InsurerId, dto.InsurerName, dto.ReferenceMonth, dto.FileName, dto.Status,
            dto.TotalRows, dto.Entered, dto.Left, dto.Changed, dto.IsFirstImport, dto.CreatedAt, changes, totals);
    }

    public async Task<IReadOnlyList<PortfolioRowDto>?> ListRowsAsync(Guid importId, CancellationToken ct = default)
    {
        var import = await db.PortfolioImports.AsNoTracking().Include(x => x.Rows).FirstOrDefaultAsync(x => x.Id == importId, ct);
        if (import is null)
        {
            return null;
        }

        var clientsByCpf = await ClientIdsByCpf(import.Rows.Select(r => r.HolderCpf), ct);
        return import.Rows
            .OrderBy(r => r.HolderName).ThenBy(r => r.PolicyNumber)
            .Select(r => new PortfolioRowDto(
                r.Id, r.ToRow().DisplayId, r.PolicyNumber, r.HolderName, r.HolderCpf, r.ProductName, r.MonthlyPremium, r.Status,
                r.StartDate, r.City, r.Competence, r.Installment, r.CommissionAmount, r.CommissionRate, r.Coverages, r.Extras,
                r.HolderCpf is { } cpf && clientsByCpf.TryGetValue(cpf, out var clientId) ? clientId : null))
            .ToList();
    }

    internal async Task<IReadOnlyList<PortfolioChangeDto>> RecentExitsAsync(int take, CancellationToken ct)
    {
        var imports = await db.PortfolioImports.AsNoTracking().Include(i => i.Changes).ToListAsync(ct);
        var insurers = await InsurerNames(ct);
        var exits = imports
            .OrderByDescending(i => i.ReferenceMonth)
            .SelectMany(i => i.Changes.Where(c => c.Type == PortfolioChangeType.Left).Select(c => (Import: i, Change: c)))
            .Take(take)
            .ToList();
        var clientsByCpf = await ClientIdsByCpf(exits.Select(e => e.Change.HolderCpf), ct);
        return exits.Select(e => ToChangeDto(e.Change, e.Import, insurers.GetValueOrDefault(e.Import.InsurerId, "?"), clientsByCpf)).ToList();
    }

    private async Task<Dictionary<Guid, string>> InsurerNames(CancellationToken ct) =>
        await db.Insurers.AsNoTracking().ToDictionaryAsync(i => i.Id, i => i.Name, ct);

    /// <summary>Relaciona CPFs da planilha com clientes cadastrados (carregado em memoria: carteira pequena).</summary>
    private async Task<Dictionary<string, Guid>> ClientIdsByCpf(IEnumerable<string?> cpfs, CancellationToken ct)
    {
        var wanted = cpfs.Where(c => !string.IsNullOrEmpty(c)).Select(c => c!).ToHashSet();
        if (wanted.Count == 0)
        {
            return [];
        }

        var all = await db.Clients.AsNoTracking().Select(c => new { c.Id, c.Cpf }).ToListAsync(ct);
        return all.Where(c => wanted.Contains(c.Cpf.Value)).ToDictionary(c => c.Cpf.Value, c => c.Id);
    }

    private static PortfolioImportDto ToDto(PortfolioImport i, Dictionary<Guid, string> insurers) => new(
        i.Id, i.InsurerId, insurers.GetValueOrDefault(i.InsurerId, "?"), i.ReferenceMonth, i.FileName, i.Status, i.TotalRows,
        i.Changes.Count(c => c.Type == PortfolioChangeType.Entered),
        i.Changes.Count(c => c.Type == PortfolioChangeType.Left),
        i.Changes.Count(c => c.Type == PortfolioChangeType.Changed),
        i.IsFirstImport, i.CreatedAt);

    private static PortfolioChangeDto ToChangeDto(PortfolioChange c, PortfolioImport i, string insurerName, Dictionary<string, Guid> clients) => new(
        c.Id, i.Id, i.ReferenceMonth, insurerName, c.Type, c.PolicyNumber, c.HolderName, c.HolderCpf, c.Detail,
        c.HolderCpf is { } cpf && clients.TryGetValue(cpf, out var clientId) ? clientId : null);
}

internal sealed class DashboardQueries(VittaDbContext db, PortfolioQueries portfolio) : IDashboardQueries
{
    public async Task<DashboardDto> GetAsync(DateOnly today, CancellationToken ct = default)
    {
        var activePolicies = await db.Policies.AsNoTracking()
            .Where(p => p.Status == PolicyStatus.Active)
            .Select(p => new { p.ClientId, p.InsurerId, p.MonthlyPremium })
            .ToListAsync(ct);
        var insurers = await db.Insurers.AsNoTracking().ToDictionaryAsync(i => i.Id, i => i.Name, ct);

        var prospects = await db.Prospects.AsNoTracking().ToListAsync(ct);
        var open = prospects.Where(p => p.IsOpen).ToList();

        var lastImport = await db.PortfolioImports.AsNoTracking().Select(i => (DateTimeOffset?)i.CreatedAt).ToListAsync(ct);

        return new DashboardDto(
            ActiveClients: activePolicies.Select(p => p.ClientId).Distinct().Count(),
            TotalClients: await db.Clients.CountAsync(ct),
            ActivePolicies: activePolicies.Count,
            MonthlyPremiumTotal: activePolicies.Sum(p => p.MonthlyPremium),
            OpenProspects: open.Count,
            FollowUpsDue: open.Where(p => p.NextFollowUpOn <= today).OrderBy(p => p.NextFollowUpOn).Select(ProspectDto.From).ToList(),
            ProspectsByStage: Enum.GetValues<ProspectStage>().Select(s => new StageCountDto(s, prospects.Count(p => p.Stage == s))).ToList(),
            PoliciesByInsurer: activePolicies.GroupBy(p => p.InsurerId)
                .Select(g => new InsurerSummaryDto(g.Key, insurers.GetValueOrDefault(g.Key, "?"), g.Count(), g.Sum(x => x.MonthlyPremium)))
                .OrderByDescending(x => x.MonthlyPremium).ToList(),
            RecentExits: await portfolio.RecentExitsAsync(10, ct),
            LastImportAt: lastImport.Max(),
            LastMonth: await LastMonthAsync(insurers, ct));
    }

    private async Task<LastMonthCommissionDto?> LastMonthAsync(Dictionary<Guid, string> insurers, CancellationToken ct)
    {
        var months = await db.PortfolioImports.AsNoTracking().Select(i => new { i.Id, i.InsurerId, i.ReferenceMonth }).ToListAsync(ct);
        if (months.Count == 0)
        {
            return null;
        }

        var latest = months.Max(m => m.ReferenceMonth);
        var current = months.Where(m => m.ReferenceMonth == latest).ToList();
        // Para cada seguradora do ultimo mes, a importacao imediatamente anterior dela.
        var previous = current
            .Select(c => months.Where(m => m.InsurerId == c.InsurerId && m.ReferenceMonth < latest).MaxBy(m => m.ReferenceMonth))
            .Where(m => m is not null)
            .Select(m => m!.Id)
            .ToList();

        var ids = current.Select(c => c.Id).Concat(previous).ToList();
        var imports = await db.PortfolioImports.AsNoTracking().Include(i => i.Rows).Where(i => ids.Contains(i.Id)).ToListAsync(ct);
        var totals = imports.ToDictionary(i => i.Id, i => MonthTotalsDto.From(i.ReferenceMonth, i.Rows.Select(r => r.ToRow()).ToList()));

        static decimal? SumCommission(IEnumerable<MonthTotalsDto> t)
        {
            var withCommission = t.Where(x => x.Commission is not null).ToList();
            return withCommission.Count > 0 ? withCommission.Sum(x => x.Commission!.Value) : null;
        }

        var now = current.Select(c => totals[c.Id]).ToList();
        var before = previous.Select(id => totals[id]).ToList();
        return new LastMonthCommissionDto(
            latest,
            SumCommission(now),
            now.Sum(t => t.Premium),
            now.Sum(t => t.Policies),
            current.Select(c => insurers.GetValueOrDefault(c.InsurerId, "?")).Distinct().OrderBy(n => n).ToList(),
            current.Count == 1 ? current[0].Id : null,
            before.Count > 0 ? before.Max(t => t.Month) : null,
            before.Count > 0 ? SumCommission(before) : null);
    }
}

internal sealed class ImportTemplateQueries(VittaDbContext db) : IImportTemplateQueries
{
    public async Task<IReadOnlyList<ImportTemplateDto>> ListAsync(ImportTemplateKind? kind, CancellationToken ct = default)
    {
        var query = db.ImportTemplates.AsNoTracking();
        if (kind is { } k)
        {
            query = query.Where(t => t.Kind == k);
        }

        var templates = await query.ToListAsync(ct);
        var insurers = await db.Insurers.AsNoTracking().ToDictionaryAsync(i => i.Id, i => i.Name, ct);
        return templates.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).Select(t => ToDto(t, insurers)).ToList();
    }

    public async Task<ImportTemplateDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var template = await db.ImportTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
        {
            return null;
        }

        var insurers = await db.Insurers.AsNoTracking().ToDictionaryAsync(i => i.Id, i => i.Name, ct);
        return ToDto(template, insurers);
    }

    private static ImportTemplateDto ToDto(ImportTemplate t, IReadOnlyDictionary<Guid, string> insurers) => new(
        t.Id, t.Name, t.Kind, t.InsurerId, t.InsurerId is { } id && insurers.TryGetValue(id, out var name) ? name : null,
        t.SheetName, t.HeaderRow, t.Columns, t.CreatedByAi, t.AiNotes, t.CreatedAt, t.LastUsedAt);
}
