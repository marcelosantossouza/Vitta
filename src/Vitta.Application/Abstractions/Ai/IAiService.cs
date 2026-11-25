using Vitta.Domain.Imports;

namespace Vitta.Application.Abstractions.Ai;

/// <summary>
/// Porta para a camada de IA (servico Python em src/Vitta.AI). A Application so conhece esta interface;
/// a implementacao HTTP fica em Vitta.Infrastructure.
/// </summary>
public interface IAiService
{
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    /// <summary>Gera um resumo em linguagem natural das mudancas de carteira de um mes.</summary>
    Task<string> SummarizePortfolioChangesAsync(string insurerName, DateOnly referenceMonth, IReadOnlyList<string> changes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pede para a IA entender o layout de uma planilha: onde esta o cabecalho e qual coluna alimenta cada campo.
    /// Lanca <see cref="AiUnavailableException"/> se o servico nao responder.
    /// </summary>
    Task<SpreadsheetSuggestion> AnalyzeSpreadsheetAsync(SpreadsheetAnalysisRequest request, CancellationToken cancellationToken = default);

    /// <summary>Provedores que o servico de IA conhece e se cada um tem chave configurada. Lanca <see cref="AiUnavailableException"/>.</summary>
    Task<AiProvidersInfo> GetProvidersAsync(CancellationToken cancellationToken = default);

    /// <summary>Chamada minima para conferir se provedor + modelo respondem. Lanca <see cref="AiUnavailableException"/> se o servico estiver fora.</summary>
    Task<AiConnectionTest> TestConnectionAsync(LlmChoice choice, CancellationToken cancellationToken = default);
}

/// <summary>Qual IA usar num pedido. Nulo = padroes do .env do servico de IA.</summary>
/// <param name="Provider">"anthropic" ou "groq".</param>
public sealed record LlmChoice(string Provider, string Model, string Effort);

public sealed record AiModelOption(string Id, string Label, string Note);

public sealed record AiProviderInfo(string Id, string Label, bool Configured, string DefaultModel, IReadOnlyList<AiModelOption> Models);

public sealed record AiProvidersInfo(string DefaultProvider, string DefaultEffort, IReadOnlyList<AiProviderInfo> Providers);

public sealed record AiConnectionTest(bool Ok, string Provider, string Model, int? LatencyMs, string Message);

/// <param name="Rows">Primeiras linhas da aba, ja mascaradas (digitos e e-mails) para nao expor dados pessoais.</param>
public sealed record SpreadsheetAnalysisRequest(
    ImportTemplateKind Kind, string FileName, string? SheetName, IReadOnlyList<string[]> Rows, IReadOnlyList<ImportField> Fields,
    LlmChoice? Llm = null);

/// <param name="AnalyzedBy">"ai" quando um LLM respondeu; "heuristic" quando foi feito por nomes de coluna.</param>
public sealed record SpreadsheetSuggestion(
    int HeaderRow, IReadOnlyList<SuggestedColumn> Columns, string? SuggestedName, string? Notes, string Confidence, string AnalyzedBy);

/// <param name="ColumnNumber">Coluna na planilha, comecando em 1 (A = 1).</param>
public sealed record SuggestedColumn(string Field, int ColumnNumber);

public sealed class AiUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
