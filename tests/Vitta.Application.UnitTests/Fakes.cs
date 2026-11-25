using Vitta.Domain.Settings;
using Vitta.Application.Abstractions.Ai;
using Vitta.Application.Abstractions.Imports;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Clients;
using Vitta.Domain.Common;
using Vitta.Domain.Imports;
using Vitta.Domain.Prospects;

namespace Vitta.Application.UnitTests;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(++SaveCount);
}

internal sealed class FakeClientRepository : IClientRepository
{
    public List<Client> Clients { get; } = [];
    public HashSet<Guid> WithPolicies { get; } = [];

    public Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Clients.FirstOrDefault(c => c.Id == id));
    public Task<bool> ExistsByCpfAsync(Cpf cpf, CancellationToken ct = default) => Task.FromResult(Clients.Any(c => c.Cpf == cpf));
    public Task<bool> HasPoliciesAsync(Guid clientId, CancellationToken ct = default) => Task.FromResult(WithPolicies.Contains(clientId));
    public Task<IReadOnlyDictionary<string, Guid>> IdsByCpfAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, Guid>>(Clients.ToDictionary(c => c.Cpf.Value, c => c.Id));
    public void Add(Client client) => Clients.Add(client);
    public void Remove(Client client) => Clients.Remove(client);
}

internal sealed class FakeProspectRepository : IProspectRepository
{
    public List<Prospect> Prospects { get; } = [];

    public Task<Prospect?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Prospects.FirstOrDefault(p => p.Id == id));
    public Task<IReadOnlyList<Prospect>> ListAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Prospect>>(Prospects.ToList());
    public void Add(Prospect prospect) => Prospects.Add(prospect);
    public void Remove(Prospect prospect) => Prospects.Remove(prospect);
}

internal sealed class FakeImportTemplateRepository : IImportTemplateRepository
{
    public List<ImportTemplate> Templates { get; } = [];

    public Task<ImportTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Templates.FirstOrDefault(t => t.Id == id));
    public Task<bool> NameExistsAsync(ImportTemplateKind kind, string name, Guid? exceptId, CancellationToken ct = default) =>
        Task.FromResult(Templates.Any(t => t.Kind == kind && t.Id != exceptId && TextNormalizer.ForSearch(t.Name) == TextNormalizer.ForSearch(name)));
    public void Add(ImportTemplate template) => Templates.Add(template);
    public void Remove(ImportTemplate template) => Templates.Remove(template);
}

/// <summary>Leitor que devolve linhas fixas (o leitor real e testado em Infrastructure).</summary>
internal sealed class FakeSpreadsheetReader(params string[][] rows) : ISpreadsheetReader
{
    public string? RequestedSheet { get; private set; }

    public Task<Result<SpreadsheetData>> ReadAsync(Stream file, string fileName, string? sheetName = null, CancellationToken ct = default)
    {
        RequestedSheet = sheetName;
        return Task.FromResult<Result<SpreadsheetData>>(new SpreadsheetData(["Plan1"], "Plan1", rows));
    }
}

internal sealed class FakeAiService(Func<SpreadsheetAnalysisRequest, SpreadsheetSuggestion>? analyze = null) : IAiService
{
    public SpreadsheetAnalysisRequest? LastRequest { get; private set; }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) => Task.FromResult(analyze is not null);
    public Task<string> SummarizePortfolioChangesAsync(string insurerName, DateOnly referenceMonth, IReadOnlyList<string> changes, CancellationToken cancellationToken = default) =>
        Task.FromResult(string.Empty);

    public Task<SpreadsheetSuggestion> AnalyzeSpreadsheetAsync(SpreadsheetAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return analyze is null ? throw new AiUnavailableException("offline") : Task.FromResult(analyze(request));
    }

    public LlmChoice? LastTest { get; private set; }

    public Task<AiProvidersInfo> GetProvidersAsync(CancellationToken cancellationToken = default) =>
        analyze is null
            ? throw new AiUnavailableException("offline")
            : Task.FromResult(new AiProvidersInfo("anthropic", "medium",
                [new("anthropic", "Anthropic (Claude)", true, "claude-opus-5-5", []), new("groq", "Groq", false, "openai/gpt-oss-120b", [])]));

    public Task<AiConnectionTest> TestConnectionAsync(LlmChoice choice, CancellationToken cancellationToken = default)
    {
        LastTest = choice;
        return analyze is null
            ? throw new AiUnavailableException("offline")
            : Task.FromResult(new AiConnectionTest(true, choice.Provider, choice.Model, 120, "ok"));
    }
}

internal sealed class FakeAiSettingsRepository(AiSettings? saved = null) : IAiSettingsRepository
{
    public AiSettings? Saved { get; private set; } = saved;
    public Task<AiSettings?> GetAsync(CancellationToken ct = default) => Task.FromResult(Saved);
    public void Add(AiSettings settings) => Saved = settings;
}
