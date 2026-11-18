using Vitta.Domain.Portfolio;

namespace Vitta.Application.Abstractions.Portfolio;

/// <summary>
/// Le a planilha de carteira de uma seguradora e normaliza em <see cref="PortfolioRow"/>.
/// Cada seguradora (MAG, Prudential...) pode ter um layout diferente: uma implementacao por layout.
/// </summary>
public interface IPortfolioSpreadsheetReader
{
    /// <summary>Codigo da seguradora atendida (ex.: "MAG").</summary>
    string InsurerCode { get; }

    Task<IReadOnlyList<PortfolioRow>> ReadAsync(Stream file, CancellationToken cancellationToken = default);
}
