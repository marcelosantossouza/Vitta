using Vitta.Domain.Common;

namespace Vitta.Domain.Portfolio;

public enum PortfolioImportStatus
{
    Pending = 1,
    Processed = 2,
    Failed = 3,
}

/// <summary>
/// Importacao mensal da planilha de carteira enviada pela seguradora (ex.: relatorio da MAG).
/// Cada importacao e comparada com a anterior para detectar entradas e saidas de clientes.
/// </summary>
public sealed class PortfolioImport : AggregateRoot
{
    private readonly List<PortfolioChange> _changes = [];

    private PortfolioImport() { } // EF

    private PortfolioImport(Guid insurerId, DateOnly referenceMonth, string fileName)
    {
        InsurerId = insurerId;
        ReferenceMonth = referenceMonth;
        FileName = fileName;
        Status = PortfolioImportStatus.Pending;
    }

    public Guid InsurerId { get; private set; }
    /// <summary>Primeiro dia do mes de referencia.</summary>
    public DateOnly ReferenceMonth { get; private set; }
    public string FileName { get; private set; } = null!;
    public PortfolioImportStatus Status { get; private set; }
    public int TotalRows { get; private set; }
    public IReadOnlyCollection<PortfolioChange> Changes => _changes.AsReadOnly();

    public static PortfolioImport Create(Guid insurerId, DateOnly referenceMonth, string fileName) =>
        new(insurerId, new DateOnly(referenceMonth.Year, referenceMonth.Month, 1), fileName);

    public void Complete(int totalRows, IEnumerable<PortfolioChange> changes)
    {
        TotalRows = totalRows;
        _changes.Clear();
        _changes.AddRange(changes);
        Status = PortfolioImportStatus.Processed;
        Touch();
    }

    public void Fail()
    {
        Status = PortfolioImportStatus.Failed;
        Touch();
    }
}
