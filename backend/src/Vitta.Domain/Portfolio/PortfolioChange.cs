using Vitta.Domain.Common;

namespace Vitta.Domain.Portfolio;

public enum PortfolioChangeType
{
    /// <summary>Apolice apareceu na carteira (venda nova ou transferida para a corretora).</summary>
    Entered = 1,
    /// <summary>Apolice sumiu da carteira (cancelamento, caducidade ou troca de corretor).</summary>
    Left = 2,
    /// <summary>Apolice continua, mas algum dado relevante mudou (premio, status...).</summary>
    Changed = 3,
}

/// <summary>Diferenca detectada entre duas importacoes consecutivas de carteira.</summary>
public sealed class PortfolioChange : Entity
{
    private PortfolioChange() { } // EF

    public PortfolioChange(PortfolioChangeType type, string policyNumber, string holderName, string? holderCpf, string? detail)
    {
        Type = type;
        PolicyNumber = policyNumber;
        HolderName = holderName;
        HolderCpf = holderCpf;
        Detail = detail;
    }

    public PortfolioChangeType Type { get; private set; }
    public string PolicyNumber { get; private set; } = null!;
    public string HolderName { get; private set; } = null!;
    public string? HolderCpf { get; private set; }
    /// <summary>Motivo/descricao (ex.: "Status: Cancelada", "Premio 120,00 -> 150,00").</summary>
    public string? Detail { get; private set; }
}
