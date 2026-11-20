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
}

/// <param name="Rows">Primeiras linhas da aba, ja mascaradas (digitos e e-mails) para nao expor dados pessoais.</param>
public sealed record SpreadsheetAnalysisRequest(
    ImportTemplateKind Kind, string FileName, string? SheetName, IReadOnlyList<string[]> Rows, IReadOnlyList<ImportField> Fields);

/// <param name="AnalyzedBy">"ai" quando um LLM respondeu; "heuristic" quando foi feito por nomes de coluna.</param>
public sealed record SpreadsheetSuggestion(
    int HeaderRow, IReadOnlyList<SuggestedColumn> Columns, string? SuggestedName, string? Notes, string Confidence, string AnalyzedBy);

/// <param name="ColumnNumber">Coluna na planilha, comecando em 1 (A = 1).</param>
public sealed record SuggestedColumn(string Field, int ColumnNumber);

public sealed class AiUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
