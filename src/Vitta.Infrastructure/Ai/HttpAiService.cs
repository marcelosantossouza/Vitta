using System.Net.Http.Json;
using Vitta.Application.Abstractions.Ai;

namespace Vitta.Infrastructure.Ai;

/// <summary>Cliente HTTP para o servico Python de IA (FastAPI em /ai).</summary>
internal sealed class HttpAiService(HttpClient http) : IAiService
{
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
        using var response = await http.PostAsJsonAsync("v1/portfolio/summarize", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SummarizeResponse>(cancellationToken);
        return body?.Summary ?? string.Empty;
    }

    private sealed record SummarizeRequest(string InsurerName, string ReferenceMonth, IReadOnlyList<string> Changes);
    private sealed record SummarizeResponse(string Summary);
}
