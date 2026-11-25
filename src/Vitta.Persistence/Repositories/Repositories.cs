using Microsoft.EntityFrameworkCore;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Clients;
using Vitta.Domain.Common;
using Vitta.Domain.Imports;
using Vitta.Domain.Insurers;
using Vitta.Domain.Policies;
using Vitta.Domain.Portfolio;
using Vitta.Domain.Prospects;
using Vitta.Domain.Settings;
using Vitta.Domain.Users;
using Vitta.Persistence.Context;

namespace Vitta.Persistence.Repositories;

internal sealed class ClientRepository(VittaDbContext db) : IClientRepository
{
    public Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct);
    public Task<bool> ExistsByCpfAsync(Cpf cpf, CancellationToken ct = default) => db.Clients.AnyAsync(c => c.Cpf == cpf, ct);
    public Task<bool> HasPoliciesAsync(Guid clientId, CancellationToken ct = default) => db.Policies.AnyAsync(p => p.ClientId == clientId, ct);

    public async Task<IReadOnlyDictionary<string, Guid>> IdsByCpfAsync(CancellationToken ct = default)
    {
        var all = await db.Clients.AsNoTracking().Select(c => new { c.Id, c.Cpf }).ToListAsync(ct);
        return all.ToDictionary(c => c.Cpf.Value, c => c.Id);
    }
    public void Add(Client client) => db.Clients.Add(client);
    public void Remove(Client client) => db.Clients.Remove(client);
}

internal sealed class PolicyRepository(VittaDbContext db) : IPolicyRepository
{
    public Task<Policy?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.Policies.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> NumberExistsAsync(Guid insurerId, string policyNumber, CancellationToken ct = default) =>
        db.Policies.AnyAsync(p => p.InsurerId == insurerId && p.PolicyNumber == policyNumber, ct);

    public async Task<IReadOnlyList<PolicySyncCandidate>> ListForCarrierSyncAsync(Guid insurerId, CancellationToken ct = default)
    {
        var rows = await (
            from p in db.Policies
            where p.InsurerId == insurerId
            join c in db.Clients on p.ClientId equals c.Id
            select new { Policy = p, c.Cpf }).ToListAsync(ct);
        return rows.Select(r => new PolicySyncCandidate(r.Policy, r.Cpf.Value)).ToList();
    }

    public void Add(Policy policy) => db.Policies.Add(policy);
}

internal sealed class ProspectRepository(VittaDbContext db) : IProspectRepository
{
    public Task<Prospect?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.Prospects.FirstOrDefaultAsync(p => p.Id == id, ct);
    public async Task<IReadOnlyList<Prospect>> ListAllAsync(CancellationToken ct = default) => await db.Prospects.AsNoTracking().ToListAsync(ct);
    public void Add(Prospect prospect) => db.Prospects.Add(prospect);
    public void Remove(Prospect prospect) => db.Prospects.Remove(prospect);
}

internal sealed class InsurerRepository(VittaDbContext db) : IInsurerRepository
{
    public async Task<IReadOnlyList<Insurer>> ListActiveAsync(CancellationToken ct = default) =>
        await db.Insurers.AsNoTracking().Where(i => i.IsActive).OrderBy(i => i.Name).ToListAsync(ct);

    public Task<Insurer?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.Insurers.FirstOrDefaultAsync(i => i.Id == id, ct);
    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default) => db.Insurers.AnyAsync(i => i.Code == code, ct);
    public void Add(Insurer insurer) => db.Insurers.Add(insurer);
}

internal sealed class PortfolioImportRepository(VittaDbContext db) : IPortfolioImportRepository
{
    public Task<PortfolioImport?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.PortfolioImports.FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<PortfolioImport?> GetWithRowsAsync(Guid id, CancellationToken ct = default) =>
        db.PortfolioImports.AsNoTracking().Include(i => i.Rows).FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<bool> ExistsAsync(Guid insurerId, DateOnly referenceMonth, CancellationToken ct = default) =>
        db.PortfolioImports.AnyAsync(i => i.InsurerId == insurerId && i.ReferenceMonth == referenceMonth, ct);

    public Task<PortfolioImport?> GetPreviousWithRowsAsync(Guid insurerId, DateOnly referenceMonth, CancellationToken ct = default) =>
        db.PortfolioImports.AsNoTracking()
            .Include(i => i.Rows)
            .Where(i => i.InsurerId == insurerId && i.ReferenceMonth < referenceMonth && i.Status == PortfolioImportStatus.Processed)
            .OrderByDescending(i => i.ReferenceMonth)
            .FirstOrDefaultAsync(ct);

    public void Add(PortfolioImport import) => db.PortfolioImports.Add(import);
    public void Remove(PortfolioImport import) => db.PortfolioImports.Remove(import);
}

internal sealed class UserRepository(VittaDbContext db) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
    public Task<bool> AnyAsync(CancellationToken ct = default) => db.Users.AnyAsync(ct);
    public void Add(User user) => db.Users.Add(user);
}

internal sealed class ImportTemplateRepository(VittaDbContext db) : IImportTemplateRepository
{
    public Task<ImportTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.ImportTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<bool> NameExistsAsync(ImportTemplateKind kind, string name, Guid? exceptId, CancellationToken ct = default)
    {
        var target = TextNormalizer.ForSearch(name);
        var names = await db.ImportTemplates.AsNoTracking()
            .Where(t => t.Kind == kind && (exceptId == null || t.Id != exceptId))
            .Select(t => t.Name)
            .ToListAsync(ct);
        return names.Any(n => TextNormalizer.ForSearch(n) == target);
    }

    public void Add(ImportTemplate template) => db.ImportTemplates.Add(template);
    public void Remove(ImportTemplate template) => db.ImportTemplates.Remove(template);
}

internal sealed class AiSettingsRepository(VittaDbContext db) : IAiSettingsRepository
{
    public Task<AiSettings?> GetAsync(CancellationToken ct = default) => db.AiSettings.FirstOrDefaultAsync(x => x.Id == AiSettings.SingletonId, ct);
    public void Add(AiSettings settings) => db.AiSettings.Add(settings);
}

internal sealed class NotificationSettingsRepository(VittaDbContext db) : INotificationSettingsRepository
{
    public Task<NotificationSettings?> GetAsync(CancellationToken ct = default) =>
        db.NotificationSettings.FirstOrDefaultAsync(x => x.Id == NotificationSettings.SingletonId, ct);
    public void Add(NotificationSettings settings) => db.NotificationSettings.Add(settings);
}
