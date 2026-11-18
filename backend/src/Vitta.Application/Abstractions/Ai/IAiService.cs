namespace Vitta.Application.Abstractions.Ai;

/// <summary>
/// Porta para a camada de IA (servico Python em /ai). A Application so conhece esta interface;
/// a implementacao HTTP fica em Vitta.Infrastructure.
/// </summary>
public interface IAiService
{
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    /// <summary>Gera um resumo em linguagem natural das mudancas de carteira de um mes.</summary>
    Task<string> SummarizePortfolioChangesAsync(string insurerName, DateOnly referenceMonth, IReadOnlyList<string> changes, CancellationToken cancellationToken = default);
}
