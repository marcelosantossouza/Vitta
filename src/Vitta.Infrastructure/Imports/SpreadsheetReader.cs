using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Vitta.Application.Abstractions.Imports;
using Vitta.Domain.Common;
using Vitta.Domain.Imports;

namespace Vitta.Infrastructure.Imports;

/// <summary>
/// Le .xlsx (ClosedXML) e .csv como texto puro, linha a linha a partir da linha 1, sem interpretar colunas.
/// Quem da significado as colunas e o modelo de planilha (ver SpreadsheetMapper na Application).
/// </summary>
internal sealed partial class SpreadsheetReader : ISpreadsheetReader
{
    private const int MaxRows = 100_000;

    public async Task<Result<SpreadsheetData>> ReadAsync(Stream file, string fileName, string? sheetName = null, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        try
        {
            if (!file.CanSeek)
            {
                var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, ct);
                buffer.Position = 0;
                file = buffer;
            }

            // A extensao pode mentir (ex.: a MAG manda .xlsx com nome .csv): o conteudo decide quando da para saber.
            extension = await SniffAsync(file, ct) ?? extension;

            var data = extension switch
            {
                ".xlsx" or ".xlsm" => ReadXlsx(file, sheetName),
                ".csv" => await ReadCsvAsync(file, pasted: false, ct),
                ".txt" or ".tsv" => await ReadCsvAsync(file, pasted: true, ct),
                ".xls" => throw new FormatException("Arquivos .xls (Excel antigo) não são suportados. Abra no Excel e salve como .xlsx."),
                _ => throw new FormatException("Formato não suportado. Envie .xlsx ou .csv, ou cole o texto copiado do site."),
            };
            return data.Rows.Count == 0 ? SpreadsheetErrors.EmptyFile : data;
        }
        catch (FormatException ex)
        {
            return SpreadsheetErrors.InvalidFile(ex.Message);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException or InvalidOperationException)
        {
            return SpreadsheetErrors.InvalidFile("Não foi possível ler a planilha. Verifique se o arquivo não está corrompido ou protegido por senha.");
        }
    }

    /// <summary>Formato real pela assinatura dos primeiros bytes: zip (PK) = .xlsx, OLE2 = .xls antigo; nulo = texto.</summary>
    private static async Task<string?> SniffAsync(Stream file, CancellationToken ct)
    {
        var start = file.Position;
        var header = new byte[4];
        var read = await file.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        file.Position = start;
        if (read < 4)
        {
            return null;
        }

        return header switch
        {
            [0x50, 0x4B, 0x03, 0x04] => ".xlsx",
            [0xD0, 0xCF, 0x11, 0xE0] => ".xls",
            _ => null,
        };
    }

    private static SpreadsheetData ReadXlsx(Stream file, string? sheetName)
    {
        using var workbook = new XLWorkbook(file);
        var visible = workbook.Worksheets.Where(w => w.Visibility == XLWorksheetVisibility.Visible).ToList();
        var sheets = (visible.Count > 0 ? visible : workbook.Worksheets.ToList()).ToList();
        var sheet = sheets.FirstOrDefault(s => string.Equals(s.Name, sheetName?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? sheets.First();

        var lastRow = Math.Min(sheet.LastRowUsed()?.RowNumber() ?? 0, MaxRows);
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var rows = new List<string[]>(lastRow);
        // Comeca na linha/coluna 1 para que "linha do cabecalho" bata com o numero da linha no Excel.
        for (var r = 1; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            rows.Add(Enumerable.Range(1, lastCol).Select(c => CellText(row.Cell(c))).ToArray());
        }

        return new SpreadsheetData(sheets.Select(s => s.Name).ToList(), sheet.Name, TrimTrailingEmpty(rows));
    }

    private static string CellText(IXLCell cell)
    {
        try
        {
            return cell.DataType switch
            {
                XLDataType.Number => cell.GetDouble().ToString("0.##########", CultureInfo.InvariantCulture),
                XLDataType.DateTime => cell.GetDateTime() is var d && d.TimeOfDay == TimeSpan.Zero
                    ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                XLDataType.Blank => string.Empty,
                _ => cell.GetFormattedString(),
            };
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException)
        {
            return cell.GetFormattedString();
        }
    }

    /// <param name="pasted">.txt = texto copiado de uma tabela do site da seguradora e colado no Vitta.</param>
    private static async Task<SpreadsheetData> ReadCsvAsync(Stream file, bool pasted, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var text = Decode(buffer.ToArray());

        var lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None).Take(MaxRows).ToList();
        if (pasted)
        {
            return new SpreadsheetData([], null, ParsePasted(lines));
        }

        var sample = lines.FirstOrDefault(l => l.Trim().Length > 0) ?? string.Empty;
        // Excel em pt-BR exporta CSV com ";" - detecta pelo primeiro conteudo.
        var separator = new[] { ';', ',', '\t', '|' }.MaxBy(c => sample.Count(ch => ch == c));
        var rows = lines.Select(l => SplitCsvLine(l, separator)).ToList();
        return new SpreadsheetData([], null, TrimTrailingEmpty(rows));
    }

    /// <summary>
    /// Tabela copiada do navegador: celulas separadas por TAB, uma linha por registro. Alguns sites quebram a ultima celula
    /// (ex.: "Ativo") numa linha sozinha e deixam linhas em branco entre os registros - aqui tudo isso e juntado de volta.
    /// Sem TAB nenhum (colado de outro lugar), separa por 2+ espacos.
    /// </summary>
    internal static List<string[]> ParsePasted(IReadOnlyList<string> lines)
    {
        var useTabs = lines.Any(l => l.Contains('\t'));
        var rows = new List<string[]>();
        foreach (var line in lines)
        {
            if (line.Trim().Length == 0)
            {
                continue;
            }

            var cells = useTabs
                ? line.Split('\t').Select(c => c.Trim()).ToArray()
                : MultiSpace().Split(line.Trim()).ToArray();

            // Linha com uma unica celula logo depois de um registro: e o pedaco que o site quebrou.
            var filled = cells.Count(c => c.Length > 0);
            if (filled == 1 && rows.Count > 0 && rows[^1].Count(c => c.Length > 0) > 1 && cells.Length < rows[^1].Length)
            {
                var previous = rows[^1];
                var value = cells.First(c => c.Length > 0);
                var lastFilled = Array.FindLastIndex(previous, c => c.Length > 0);
                rows[^1] = lastFilled + 1 < previous.Length
                    ? previous.Select((c, i) => i == lastFilled + 1 ? value : c).ToArray()
                    : [.. previous, value];
                continue;
            }

            rows.Add(cells);
        }

        // Tira colunas vazias no fim (TAB sobrando no final das linhas).
        var width = rows.Count == 0 ? 0 : rows.Max(r => Array.FindLastIndex(r, c => c.Length > 0) + 1);
        return rows.Select(r => r.Length > width ? r[..width] : r).ToList();
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\s{2,}")]
    private static partial System.Text.RegularExpressions.Regex MultiSpace();

    /// <summary>UTF-8 (com ou sem BOM); se nao for UTF-8 valido, assume Windows-1252/Latin-1 (padrao do Excel no Brasil).</summary>
    internal static string Decode(byte[] bytes)
    {
        try
        {
            var text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            return text.TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    private static List<string[]> TrimTrailingEmpty(List<string[]> rows)
    {
        var last = rows.FindLastIndex(r => r.Any(c => !string.IsNullOrWhiteSpace(c)));
        return rows.Take(last + 1).ToList();
    }

    internal static string[] SplitCsvLine(string line, char separator)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (ch == separator && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
