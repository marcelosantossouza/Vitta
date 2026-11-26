using Microsoft.EntityFrameworkCore;
using Vitta.Application.Features.Referrals;
using Vitta.Domain.Portfolio;
using Vitta.Persistence.Context;

namespace Vitta.Persistence.Queries;

internal sealed class ReferralQueries(VittaDbContext db) : IReferralQueries
{
    public async Task<ReferralData> LoadAsync(Guid? groupId, CancellationToken ct = default)
    {
        var groups = await db.ReferralGroups.AsNoTracking().Where(g => groupId == null || g.Id == groupId).ToListAsync(ct);
        var groupIds = groups.Select(g => g.Id).ToList();

        var members = (await db.Clients.AsNoTracking()
                .Where(c => c.ReferralGroupId != null && groupIds.Contains(c.ReferralGroupId.Value))
                .Select(c => new { c.Id, c.FullName, c.Cpf, c.ReferralGroupId })
                .ToListAsync(ct))
            .Select(c => new ReferralMemberData(c.Id, c.FullName, c.Cpf.Value, c.ReferralGroupId!.Value))
            .ToList();
        var clientIds = members.Select(m => m.ClientId).ToList();

        var policies = await (
            from p in db.Policies.AsNoTracking()
            join i in db.Insurers.AsNoTracking() on p.InsurerId equals i.Id
            where clientIds.Contains(p.ClientId)
            select new ReferralPolicyData(p.Id, p.ClientId, i.Name, p.PolicyNumber, p.ProductName, p.MonthlyPremium, p.CommissionRate,
                p.IssuedOn, p.Status, p.EndedOn)).ToListAsync(ct);

        // Comissao real das planilhas mensais das seguradoras, ligada pelo CPF do titular.
        var cpfs = members.Select(m => m.Cpf).ToHashSet();
        var cpfList = cpfs.ToList();
        var imports = await db.PortfolioImports.AsNoTracking()
            .Where(i => i.Status == PortfolioImportStatus.Processed)
            .Include(i => i.Rows.Where(r => r.HolderCpf != null && r.CommissionAmount != null && cpfList.Contains(r.HolderCpf)))
            .ToListAsync(ct);
        var imported = imports
            .SelectMany(i => i.Rows.Select(r => new ImportedCommissionData(
                r.HolderCpf!, new DateOnly(i.ReferenceMonth.Year, i.ReferenceMonth.Month, 1), r.CommissionAmount!.Value)))
            .ToList();

        return new ReferralData(groups, members, policies, imported);
    }
}
