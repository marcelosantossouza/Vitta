using Vitta.Domain.Common;

namespace Vitta.Application.Abstractions.Imports;

/// <summary>Conteudo "cru" de uma aba: todas as linhas como texto, a partir da linha 1 da planilha.</summary>
/// <param name="Sheets">Abas do .xlsx (vazio para .csv).</param>
/// <param name="SheetName">Aba efetivamente lida (nulo para .csv).</param>
public sealed record SpreadsheetData(IReadOnlyList<string> Sheets, string? SheetName, IReadOnlyList<string[]> Rows);

/// <summary>Le .xlsx/.csv sem interpretar colunas - quem da significado e o modelo de planilha.</summary>
public interface ISpreadsheetReader
{
    /// <param name="sheetName">Aba desejada; se nula ou inexistente, usa a primeira.</param>
    Task<Result<SpreadsheetData>> ReadAsync(Stream file, string fileName, string? sheetName = null, CancellationToken ct = default);
}
