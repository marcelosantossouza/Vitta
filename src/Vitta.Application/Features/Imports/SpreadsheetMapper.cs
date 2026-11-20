using System.Globalization;
using System.Text.RegularExpressions;
using Vitta.Application.Abstractions.Imports;
using Vitta.Domain.Common;
using Vitta.Domain.Imports;

namespace Vitta.Application.Features.Imports;

/// <summary>Uma linha da planilha ja traduzida para os campos do Vitta.</summary>
public sealed class MappedRow(int rowNumber, IReadOnlyList<(string Field, string Column, string Value)> cells)
{
    /// <summary>Numero da linha na planilha (1-based), para mensagens ao usuario.</summary>
    public int RowNumber { get; } = rowNumber;

    public string? Get(string field) => cells.FirstOrDefault(c => c.Field == field).Value;

    public IEnumerable<(string Column, string Value)> All(string field) =>
        cells.Where(c => c.Field == field).Select(c => (c.Column, c.Value));
}

/// <summary>
/// Aplica um modelo de planilha (linha do cabecalho + colunas) sobre o conteudo cru e oferece a heuristica
/// por nomes de coluna usada quando nao ha modelo ou a IA esta indisponivel.
/// </summary>
public static class SpreadsheetMapper
{
    public static string NormalizeHeader(string? header)
    {
        var text = TextNormalizer.ForSearch(header).Replace("º", "").Replace("°", "").Replace(".", "").Replace("_", " ").Replace(":", "");
        return string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Nome da coluna quando nao ha cabecalho (texto colado): "Coluna 1", "Coluna 2"...</summary>
    public static string PositionalName(int index) => $"Coluna {index + 1}";

    /// <summary>Cabecalhos da linha informada; linha 0 = sem cabecalho, as colunas viram "Coluna N".</summary>
    public static string[] HeadersAt(SpreadsheetData data, int headerRow) => HeadersAt(data.Rows, headerRow);

    public static string[] HeadersAt(IReadOnlyList<string[]> rows, int headerRow)
    {
        if (headerRow == 0)
        {
            var width = rows.Count == 0 ? 0 : rows.Max(r => r.Length);
            return Enumerable.Range(0, width).Select(PositionalName).ToArray();
        }

        return headerRow >= 1 && headerRow <= rows.Count ? rows[headerRow - 1].Select(h => h.Trim()).ToArray() : [];
    }

    public static Result<IReadOnlyList<MappedRow>> Apply(SpreadsheetData data, int headerRow, IReadOnlyList<ColumnMapping> columns, string templateName)
    {
        var headers = HeadersAt(data, headerRow).Select(NormalizeHeader).ToArray();
        var resolved = new List<(string Field, string Column, int Index)>();
        var missing = new List<string>();
        foreach (var mapping in columns)
        {
            var index = Array.IndexOf(headers, NormalizeHeader(mapping.Column));
            if (index < 0)
            {
                missing.Add(mapping.Column);
            }
            else
            {
                resolved.Add((mapping.Field, mapping.Column, index));
            }
        }

        if (missing.Count > 0)
        {
            return ImportTemplateErrors.ColumnsNotFound(templateName, missing);
        }

        var rows = new List<MappedRow>();
        for (var i = headerRow; i < data.Rows.Count; i++)
        {
            var row = data.Rows[i];
            var cells = resolved
                .Select(r => (r.Field, r.Column, Value: r.Index < row.Length ? row[r.Index].Trim() : string.Empty))
                .Where(c => c.Value.Length > 0)
                .ToList();
            if (cells.Count > 0)
            {
                rows.Add(new MappedRow(i + 1, cells));
            }
        }

        if (headerRow == 0)
        {
            // Sem cabecalho nao da para conferir pelo nome: confere se o conteudo ainda tem o formato esperado
            // (ex.: a coluna do CPF continua tendo CPFs). Pega o site que mudou a ordem das colunas.
            var wrong = resolved
                .Select(r => (r.Column, Field: ImportFields.Find(r.Field), Values: rows.Select(row => row.Get(r.Field)).OfType<string>().ToList()))
                .Where(r => r.Field is not null && r.Values.Count >= 3 && !Looks(r.Values, r.Field.ValueKind))
                .Select(r => $"a {r.Column} deveria ter {r.Field!.Label}")
                .ToList();
            if (wrong.Count > 0)
            {
                return ImportTemplateErrors.LayoutChanged(templateName, wrong);
            }
        }

        return rows;
    }

    // ---- Reconhecimento pelo conteudo (texto colado sem cabecalho) ----
    private static readonly Regex CpfPattern = new(@"^\d{3}\.?\d{3}\.?\d{3}-?\d{2}$", RegexOptions.Compiled);
    private static readonly Regex MoneyPattern = new(@"^(R\$\s*)?-?\d{1,3}(\.?\d{3})*,\d{2}$|^R\$\s*-?[\d.,]+$", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"^(\+?55\s*)?\(?\d{2}\)?\s*9?\s*\d{4}[\s-]?\d{4}$", RegexOptions.Compiled);
    private static readonly Regex CodePattern = new(@"^[A-Za-z]{0,3}[\d./-]{4,25}$", RegexOptions.Compiled);
    private static readonly string[] StatusWords =
        ["ativ", "cancel", "suspens", "inadimpl", "caduc", "atras", "pendent", "vigente", "encerrad", "rescind", "quitad", "em dia", "baixad"];

    /// <summary>Formato de um valor isolado (Text quando nao reconhece).</summary>
    public static ImportValueKind Classify(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length == 0)
        {
            return ImportValueKind.Text;
        }

        if (v.Contains('@') && !v.Contains(' '))
        {
            return ImportValueKind.Email;
        }

        if (CpfPattern.IsMatch(v) && (v.Contains('.') || v.Contains('-') || v.Length == 11))
        {
            return ImportValueKind.Cpf;
        }

        if (ParseDate(v) is not null)
        {
            return ImportValueKind.Date;
        }

        if (MoneyPattern.IsMatch(v))
        {
            return ImportValueKind.Money;
        }

        if (PhonePattern.IsMatch(v) && Digits(v).Length >= 10)
        {
            return ImportValueKind.Phone;
        }

        var normalized = TextNormalizer.ForSearch(v);
        if (v.Length <= 30 && StatusWords.Any(w => normalized.StartsWith(w, StringComparison.Ordinal)))
        {
            return ImportValueKind.Status;
        }

        return CodePattern.IsMatch(v) ? ImportValueKind.Code : ImportValueKind.Text;
    }

    /// <summary>Pelo menos 70% dos valores tem o formato esperado (texto e codigo aceitam qualquer coisa).</summary>
    private static bool Looks(IReadOnlyList<string> values, ImportValueKind kind)
    {
        if (kind is ImportValueKind.Text or ImportValueKind.Code or ImportValueKind.Status)
        {
            return true;
        }

        var hits = values.Count(v => Classify(v) == kind || (kind == ImportValueKind.Money && decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out _)));
        return hits >= values.Count * 0.7;
    }

    /// <summary>Parece linha de dados (e nao de titulos)? Usado para decidir que o texto colado veio sem cabecalho.</summary>
    public static bool LooksLikeData(string[] row) =>
        row.Count(c => Classify(c) is ImportValueKind.Cpf or ImportValueKind.Date or ImportValueKind.Money or ImportValueKind.Email or ImportValueKind.Phone) >= 1
        && row.Count(c => !string.IsNullOrWhiteSpace(c)) >= 2;

    /// <summary>
    /// Sugere o mapeamento olhando os VALORES de cada coluna (texto colado sem cabecalho). Colunas pela posicao ("Coluna N").
    /// </summary>
    public static List<ColumnMapping> AutoMapByContent(IReadOnlyList<string[]> rows, ImportTemplateKind kind)
    {
        var width = rows.Count == 0 ? 0 : rows.Max(r => r.Length);
        var profiles = Enumerable.Range(0, width).Select(i =>
        {
            var values = rows.Select(r => i < r.Length ? r[i].Trim() : string.Empty).Where(v => v.Length > 0).ToList();
            var kinds = values.Select(Classify).ToList();
            var dominant = kinds.GroupBy(k => k).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
            var share = values.Count == 0 ? 0 : kinds.Count(k => k == dominant) / (double)values.Count;
            return new
            {
                Index = i,
                Values = values,
                Kind = share >= 0.7 ? dominant : ImportValueKind.Text,
                Distinct = values.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                Words = values.Count == 0 ? 0 : values.Average(v => v.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length),
                HasLetters = values.Count > 0 && values.Count(v => v.Any(char.IsLetter)) >= values.Count * 0.7,
            };
        }).Where(p => p.Values.Count > 0).ToList();

        var used = new HashSet<int>();
        var result = new List<ColumnMapping>();
        void Take(string field, int? index)
        {
            if (index is { } i && used.Add(i))
            {
                result.Add(new ColumnMapping(field, PositionalName(i)));
            }
        }

        int? First(ImportValueKind valueKind) => profiles.FirstOrDefault(p => p.Kind == valueKind && !used.Contains(p.Index))?.Index;
        // Nome = coluna de texto com mais palavras (nomes de pessoa costumam ter 3+; cidades e produtos, menos).
        int? NameColumn() => profiles.Where(p => p.Kind == ImportValueKind.Text && p.HasLetters && !used.Contains(p.Index) && p.Distinct > 1)
            .OrderByDescending(p => p.Words).FirstOrDefault()?.Index;

        if (kind == ImportTemplateKind.Portfolio)
        {
            Take(ImportFields.Portfolio.HolderCpf, First(ImportValueKind.Cpf));
            Take(ImportFields.Portfolio.MonthlyPremium, First(ImportValueKind.Money));
            Take(ImportFields.Portfolio.StartDate, First(ImportValueKind.Date));
            Take(ImportFields.Portfolio.Status, First(ImportValueKind.Status));
            // Apolice: codigo que muda de linha para linha. Codigo repetido em todas (ex.: codigo do corretor) fica de fora.
            Take(ImportFields.Portfolio.PolicyNumber, profiles
                .FirstOrDefault(p => p.Kind == ImportValueKind.Code && !used.Contains(p.Index) && p.Values.Count >= 2 && p.Distinct == p.Values.Count)?.Index);
            Take(ImportFields.Portfolio.HolderName, NameColumn());
        }
        else
        {
            Take(ImportFields.Prospects.Email, First(ImportValueKind.Email));
            Take(ImportFields.Prospects.Phone, First(ImportValueKind.Phone));
            Take(ImportFields.Prospects.NextFollowUpOn, First(ImportValueKind.Date));
            Take(ImportFields.Prospects.Name, NameColumn());
            foreach (var p in profiles.Where(p => !used.Contains(p.Index) && p.HasLetters))
            {
                Take(ImportFields.Prospects.Notes, p.Index);
            }
        }

        return result.OrderBy(m => int.Parse(m.Column["Coluna ".Length..], CultureInfo.InvariantCulture)).ToList();
    }

    /// <summary>
    /// Linha (1-based) entre as primeiras 20 que mais parece cabecalho para o tipo informado; 0 quando a primeira linha
    /// ja e de dados (texto colado sem cabecalho).
    /// </summary>
    public static int DetectHeaderRow(IReadOnlyList<string[]> rows, ImportTemplateKind kind)
    {
        var best = (Row: 0, Score: 0);
        for (var i = 0; i < Math.Min(rows.Count, 20); i++)
        {
            var score = AutoMap(rows[i], kind).Count;
            if (score > best.Score)
            {
                best = (i + 1, score);
            }
        }

        if (best.Score > 0)
        {
            return best.Row;
        }

        // Texto colado do site costuma vir sem titulos: a primeira linha preenchida ja e de dados.
        var firstFilled = rows.Take(20).FirstOrDefault(r => r.Any(c => !string.IsNullOrWhiteSpace(c)));
        if (firstFilled is not null && LooksLikeData(firstFilled))
        {
            return 0;
        }

        // Sem pistas: primeira linha com pelo menos duas celulas preenchidas.
        for (var i = 0; i < Math.Min(rows.Count, 20); i++)
        {
            if (rows[i].Count(c => !string.IsNullOrWhiteSpace(c)) >= 2)
            {
                return i + 1;
            }
        }

        return 1;
    }

    /// <summary>Sugere o mapeamento comparando os cabecalhos com os nomes conhecidos de cada campo.</summary>
    public static List<ColumnMapping> AutoMap(IReadOnlyList<string> headers, ImportTemplateKind kind)
    {
        var normalized = headers.Select(NormalizeHeader).ToArray();
        var used = new HashSet<int>();
        var result = new List<ColumnMapping>();

        foreach (var field in ImportFields.For(kind))
        {
            // 1) nome identico; 2) cabecalho comecando com um dos nomes ("cpf do titular", "premio liquido").
            var matches = Enumerable.Range(0, normalized.Length)
                .Where(i => !used.Contains(i) && normalized[i].Length > 0)
                .Select(i => (Index: i, Rank: field.Synonyms.Contains(normalized[i]) ? 0
                    : field.Synonyms.Any(s => normalized[i].StartsWith(s + " ", StringComparison.Ordinal)) ? 1 : 2))
                .Where(m => m.Rank < 2)
                .OrderBy(m => m.Rank)
                .ToList();

            foreach (var match in field.Multiple ? matches : matches.Take(1))
            {
                used.Add(match.Index);
                result.Add(new ColumnMapping(field.Key, headers[match.Index].Trim()));
            }
        }

        return result;
    }

    /// <summary>Aceita "1.234,56", "1234.56", "R$ 120,00".</summary>
    public static decimal? ParseMoney(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = new string(value.Where(ch => char.IsDigit(ch) || ch is ',' or '.' or '-').ToArray());
        if (cleaned.Contains(','))
        {
            cleaned = cleaned.Replace(".", "").Replace(',', '.');
        }

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static readonly string[] DateFormats = ["dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy", "yyyy-MM-dd", "dd-MM-yyyy", "dd.MM.yyyy"];

    /// <summary>Datas no formato brasileiro ou ISO (o leitor de .xlsx ja converte datas para aaaa-mm-dd).</summary>
    public static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim().Split(' ', 'T')[0];
        return DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    }

    public static string Digits(string? value) => new((value ?? string.Empty).Where(char.IsDigit).ToArray());
}
