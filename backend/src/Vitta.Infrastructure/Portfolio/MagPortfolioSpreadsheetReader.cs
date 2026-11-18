using ClosedXML.Excel;
using Vitta.Application.Abstractions.Portfolio;
using Vitta.Domain.Portfolio;

namespace Vitta.Infrastructure.Portfolio;

/// <summary>
/// Leitor da planilha de carteira da MAG.
/// ATENCAO: nomes de colunas sao provisorios. Ajustar quando tivermos uma planilha real (anonimizada).
/// </summary>
internal sealed class MagPortfolioSpreadsheetReader : IPortfolioSpreadsheetReader
{
    private static readonly string[] PolicyHeaders = ["apolice", "apólice", "proposta", "certificado"];
    private static readonly string[] NameHeaders = ["segurado", "nome", "nome do segurado"];
    private static readonly string[] CpfHeaders = ["cpf", "cpf do segurado"];
    private static readonly string[] ProductHeaders = ["produto", "plano"];
    private static readonly string[] PremiumHeaders = ["premio", "prêmio", "premio mensal", "valor"];
    private static readonly string[] StatusHeaders = ["status", "situacao", "situação"];

    public string InsurerCode => "MAG";

    public Task<IReadOnlyList<PortfolioRow>> ReadAsync(Stream file, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(file);
        var sheet = workbook.Worksheets.First();
        var headerRow = sheet.FirstRowUsed() ?? throw new InvalidOperationException("Planilha vazia.");

        var columns = headerRow.CellsUsed().ToDictionary(c => c.GetString().Trim().ToLowerInvariant(), c => c.Address.ColumnNumber);
        int? Col(string[] names) => names.Select(n => columns.TryGetValue(n, out var i) ? i : (int?)null).FirstOrDefault(i => i is not null);

        var policyCol = Col(PolicyHeaders) ?? throw new InvalidOperationException("Coluna de apolice nao encontrada.");
        var nameCol = Col(NameHeaders) ?? throw new InvalidOperationException("Coluna de nome do segurado nao encontrada.");
        var cpfCol = Col(CpfHeaders);
        var productCol = Col(ProductHeaders);
        var premiumCol = Col(PremiumHeaders);
        var statusCol = Col(StatusHeaders);

        var rows = new List<PortfolioRow>();
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var policy = row.Cell(policyCol).GetString().Trim();
            if (string.IsNullOrEmpty(policy))
            {
                continue;
            }

            rows.Add(new PortfolioRow(
                policy,
                row.Cell(nameCol).GetString().Trim(),
                cpfCol is { } c ? new string(row.Cell(c).GetString().Where(char.IsDigit).ToArray()) : null,
                productCol is { } p ? row.Cell(p).GetString().Trim() : null,
                premiumCol is { } v && row.Cell(v).TryGetValue<decimal>(out var premium) ? premium : null,
                statusCol is { } s ? row.Cell(s).GetString().Trim() : null));
        }

        return Task.FromResult<IReadOnlyList<PortfolioRow>>(rows);
    }
}
