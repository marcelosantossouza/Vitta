using System.Net.Http.Json;
using System.Text.Json;
using Vitta.Application.Abstractions.Ai;

namespace Vitta.Infrastructure.Ai;

/// <summary>Cliente HTTP para o servico Python de IA (FastAPI em src/Vitta.AI).</summary>
internal sealed class HttpAiService(HttpClient http) : IAiService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<string> SummarizePortfolioChangesAsync(string insurerName, DateOnly referenceMonth, IReadOnlyList<string> changes, CancellationToken cancellationToken = default)
    {
        var request = new SummarizeRequest(insurerName, referenceMonth.ToString("yyyy-MM"), changes);
        using var response = await http.PostAsJsonAsync("v1/portfolio/summarize", request, Json, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SummarizeResponse>(Json, cancellationToken);
        return body?.Summary ?? string.Empty;
    }

    public async Task<SpreadsheetSuggestion> AnalyzeSpreadsheetAsync(SpreadsheetAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var body = new AnalyzeRequest(
            request.Kind.ToString(), request.FileName, request.SheetName, request.Rows,
            request.Fields.Select(f => new AnalyzeField(f.Key, f.Label, f.Required, f.Multiple, f.Description, f.Synonyms)).ToList(),
            request.Llm);
        try
        {
            using var response = await http.PostAsJsonAsync("v1/spreadsheets/analyze", body, Json, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // 503 = servico no ar mas sem chave da Anthropic (modo offline).
                throw new AiUnavailableException($"Serviço de IA respondeu {(int)response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<AnalyzeResponse>(Json, cancellationToken)
                ?? throw new AiUnavailableException("Resposta vazia do serviço de IA.");
            return new SpreadsheetSuggestion(
                result.HeaderRow,
                (result.Columns ?? []).Select(c => new SuggestedColumn(c.Field, c.ColumnNumber)).ToList(),
                result.SuggestedName, result.Notes, result.Confidence ?? "low", "ai");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException
                                       || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new AiUnavailableException("Serviço de IA indisponível.", ex);
        }
    }

    public async Task<AiProvidersInfo> GetProvidersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync("v1/llm/providers", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new AiUnavailableException($"Serviço de IA respondeu {(int)response.StatusCode}.");
            }

            return await response.Content.ReadFromJsonAsync<AiProvidersInfo>(Json, cancellationToken)
                ?? throw new AiUnavailableException("Resposta vazia do serviço de IA.");
        }
        catch (Exception ex) when (IsTransport(ex, cancellationToken))
        {
            throw new AiUnavailableException("Serviço de IA indisponível.", ex);
        }
    }

    public async Task<AiConnectionTest> TestConnectionAsync(LlmChoice choice, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("v1/llm/test", choice, Json, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new AiUnavailableException($"Serviço de IA respondeu {(int)response.StatusCode}.");
            }

            return await response.Content.ReadFromJsonAsync<AiConnectionTest>(Json, cancellationToken)
                ?? throw new AiUnavailableException("Resposta vazia do serviço de IA.");
        }
        catch (Exception ex) when (IsTransport(ex, cancellationToken))
        {
            throw new AiUnavailableException("Serviço de IA indisponível.", ex);
        }
    }

    private static bool IsTransport(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException or JsonException or NotSupportedException
        || (ex is TaskCanceledException && !ct.IsCancellationRequested);

    private sealed record SummarizeRequest(string InsurerName, string ReferenceMonth, IReadOnlyList<string> Changes);
    private sealed record SummarizeResponse(string Summary);

    private sealed record AnalyzeField(string Field, string Label, bool Required, bool Multiple, string Description, IReadOnlyList<string> Synonyms);
    private sealed record AnalyzeRequest(string Kind, string FileName, string? SheetName, IReadOnlyList<string[]> Rows, IReadOnlyList<AnalyzeField> Fields, LlmChoice? Llm);
    private sealed record AnalyzeColumn(string Field, int ColumnNumber);
    private sealed record AnalyzeResponse(int HeaderRow, IReadOnlyList<AnalyzeColumn>? Columns, string? SuggestedName, string? Notes, string? Confidence);
}
