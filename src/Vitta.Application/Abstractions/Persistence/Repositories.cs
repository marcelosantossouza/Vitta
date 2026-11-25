using Vitta.Domain.Clients;
using Vitta.Domain.Common;
using Vitta.Domain.Imports;
using Vitta.Domain.Insurers;
using Vitta.Domain.Policies;
using Vitta.Domain.Portfolio;
using Vitta.Domain.Prospects;
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
    /// <summary>Todos os prospects (sem tracking) - usado para evitar duplicados na importacao.</summary>
    Task<IReadOnlyList<Prospect>> ListAllAsync(CancellationToken ct = default);
    void Add(Prospect prospect);
    void Remove(Prospect prospect);
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
