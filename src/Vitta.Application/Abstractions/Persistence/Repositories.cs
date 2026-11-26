using Vitta.Domain.Clients;
using Vitta.Domain.Common;
using Vitta.Domain.Imports;
using Vitta.Domain.Insurers;
using Vitta.Domain.Policies;
using Vitta.Domain.Portfolio;
using Vitta.Domain.Prospects;
using Vitta.Domain.Referrals;
using Vitta.Domain.Settings;
using Vitta.Domain.Users;

namespace Vitta.Application.Abstractions.Persistence;

// Repositorios = lado de ESCRITA (carregam agregados com tracking).
// Leituras para telas ficam nas interfaces de Abstractions/Queries, que ja devolvem DTOs.

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsByCpfAsync(Cpf cpf, CancellationToken ct = default);
    Task<bool> HasPoliciesAsync(Guid clientId, CancellationToken ct = default);
    /// <summary>Id de cada cliente pelo CPF (so digitos), para ligar linhas importadas ao cadastro.</summary>
    Task<IReadOnlyDictionary<string, Guid>> IdsByCpfAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Client>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<IReadOnlyList<Client>> ListByReferralAsync(Guid referralGroupId, CancellationToken ct = default);
    void Add(Client client);
    void Remove(Client client);
}

public interface IPolicyRepository
{
    Task<Policy?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> NumberExistsAsync(Guid insurerId, string policyNumber, CancellationToken ct = default);
    /// <summary>Apolices da seguradora (com tracking) e o CPF do cliente, para atualizar com a carteira importada.</summary>
    Task<IReadOnlyList<PolicySyncCandidate>> ListForCarrierSyncAsync(Guid insurerId, CancellationToken ct = default);
    void Add(Policy policy);
}

/// <param name="ClientCpf">CPF do cliente, so digitos.</param>
public sealed record PolicySyncCandidate(Policy Policy, string ClientCpf);

public interface IProspectRepository
{
    Task<Prospect?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Todos os prospects - usado para achar duplicados na importacao (com tracking para poder atualizar).</summary>
    Task<IReadOnlyList<Prospect>> ListAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Prospect>> ListByImportAsync(Guid importId, CancellationToken ct = default);
    Task<IReadOnlyList<Prospect>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    void Add(Prospect prospect);
    void Remove(Prospect prospect);
}

public interface IReferralGroupRepository
{
    Task<ReferralGroup?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken ct = default);
    void Add(ReferralGroup group);
    void Remove(ReferralGroup group);
}

public interface IInsurerRepository
{
    Task<IReadOnlyList<Insurer>> ListActiveAsync(CancellationToken ct = default);
    Task<Insurer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
    void Add(Insurer insurer);
}

public interface IPortfolioImportRepository
{
    Task<PortfolioImport?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Importacao com as linhas carregadas (sem tracking).</summary>
    Task<PortfolioImport?> GetWithRowsAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid insurerId, DateOnly referenceMonth, CancellationToken ct = default);
    /// <summary>Importacao imediatamente anterior ao mes informado (com as linhas carregadas).</summary>
    Task<PortfolioImport?> GetPreviousWithRowsAsync(Guid insurerId, DateOnly referenceMonth, CancellationToken ct = default);
    void Add(PortfolioImport import);
    void Remove(PortfolioImport import);
}

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
    Task<IReadOnlyList<User>> ListAsync(CancellationToken ct = default);
    /// <summary>Administradores ativos (para nao deixar o sistema sem nenhum).</summary>
    Task<int> CountActiveAdminsAsync(CancellationToken ct = default);
    void Add(User user);
}

public interface IImportTemplateRepository
{
    Task<ImportTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Nome ja usado por outro modelo do mesmo tipo (sem diferenciar maiusculas/acentos).</summary>
    Task<bool> NameExistsAsync(ImportTemplateKind kind, string name, Guid? exceptId, CancellationToken ct = default);
    void Add(ImportTemplate template);
    void Remove(ImportTemplate template);
}

public interface IAiSettingsRepository
{
    /// <summary>Configuracao de IA salva (nula = ninguem salvou ainda: valem os padroes do servico de IA).</summary>
    Task<AiSettings?> GetAsync(CancellationToken ct = default);
    void Add(AiSettings settings);
}

public interface INotificationSettingsRepository
{
    /// <summary>Configuracao do e-mail diario (nula = ninguem configurou ainda).</summary>
    Task<NotificationSettings?> GetAsync(CancellationToken ct = default);
    void Add(NotificationSettings settings);
}

public interface IProspectImportRepository
{
    Task<ProspectImport?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(ProspectImport import);
    void Remove(ProspectImport import);
}

public interface ILeadCaptureSettingsRepository
{
    Task<LeadCaptureSettings?> GetAsync(CancellationToken ct = default);
    void Add(LeadCaptureSettings settings);
}
