using System.Text;
using ClosedXML.Excel;
using Vitta.Infrastructure.Imports;

namespace Vitta.Infrastructure.UnitTests;

public class SpreadsheetReaderTests
{
    private readonly SpreadsheetReader _reader = new();

    [Fact]
    public async Task ReadsSemicolonCsv_WithQuotes()
    {
        const string csv = "Apólice;Nome do Segurado;Prêmio\n00123;\"Silva; Ana\";\"1.234,56\"\n\n";

        var result = await _reader.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(csv)), "carteira.csv");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Rows.Count); // linhas vazias do final sao descartadas
        Assert.Equal(["00123", "Silva; Ana", "1.234,56"], result.Value.Rows[1]);
        Assert.Empty(result.Value.Sheets);
    }

    [Fact]
    public async Task ReadsLatin1Csv_ExportedByBrazilianExcel()
    {
        var bytes = Encoding.Latin1.GetBytes("Apólice;Situação\n1;Ativa\n");

        var result = await _reader.ReadAsync(new MemoryStream(bytes), "mag.csv");

        Assert.Equal(["Apólice", "Situação"], result.Value.Rows[0]);
    }

    [Fact]
    public async Task ReadsChosenXlsxSheet_KeepingExcelRowNumbers()
    {
        using var wb = new XLWorkbook();
        wb.AddWorksheet("Resumo").Cell(1, 1).Value = "nada";
        var ws = wb.AddWorksheet("Carteira");
        ws.Cell(3, 2).Value = "Proposta";
        ws.Cell(3, 3).Value = "Valor";
        ws.Cell(4, 2).Value = 98765;
        ws.Cell(4, 3).Value = 89.9;
        ws.Cell(5, 2).Value = new DateTime(2026, 3, 15);
        var stream = new MemoryStream();
        wb.SaveAs(stream);
        stream.Position = 0;

        var result = await _reader.ReadAsync(stream, "mag.xlsx", "carteira");

        Assert.Equal(["Resumo", "Carteira"], result.Value.Sheets);
        Assert.Equal("Carteira", result.Value.SheetName);
        Assert.Equal(5, result.Value.Rows.Count);
        Assert.Equal(["", "Proposta", "Valor"], result.Value.Rows[2]);
        Assert.Equal(["", "98765", "89.9"], result.Value.Rows[3]);
        Assert.Equal("2026-03-15", result.Value.Rows[4][1]);
    }

    [Fact]
    public async Task XlsxNamedCsv_IsReadAsXlsx()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Extrato");
        ws.Cell(1, 1).Value = "Proposta";
        ws.Cell(2, 1).Value = 12345;
        var stream = new MemoryStream();
        wb.SaveAs(stream);
        stream.Position = 0;

        var result = await _reader.ReadAsync(stream, "teste_mag.csv");

        Assert.True(result.IsSuccess);
        Assert.Equal("Extrato", result.Value.SheetName);
        Assert.Equal(["Proposta"], result.Value.Rows[0]);
        Assert.Equal(["12345"], result.Value.Rows[1]);
    }

    [Fact]
    public async Task OldXls_ReturnsFriendlyError()
    {
        var result = await _reader.ReadAsync(new MemoryStream([1, 2, 3]), "mag.xls");

        Assert.True(result.IsFailure);
        Assert.Contains(".xlsx", result.Error.Message);
    }
}

public class PastedTextTests
{
    [Fact]
    public async Task PastedTable_JoinsCellThatTheBrowserBrokeIntoItsOwnLine()
    {
        // Formato que sai ao copiar a tabela do site da MAG (dados ficticios): TAB no fim, status na linha de baixo, linha em branco.
        const string text = "7000001\t529.982.247-25\tANA LIMA\tRIO DE JANEIRO\t22/05/2017\tR$ 1.234,56\t\nAtivo\n\n" +
                            "7000001\t111.444.777-35\tBRUNO REIS\tNITERÓI\t01/08/2024\tR$ 269,31\t\nCancelado\n\n";

        var result = await new SpreadsheetReader().ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(text)), "texto-colado.txt");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Rows.Count);
        Assert.Equal(["7000001", "529.982.247-25", "ANA LIMA", "RIO DE JANEIRO", "22/05/2017", "R$ 1.234,56", "Ativo"], result.Value.Rows[0]);
        Assert.Equal("Cancelado", result.Value.Rows[1][6]);
    }

    [Fact]
    public void PastedWithoutTabs_SplitsOnRunsOfSpaces()
    {
        var rows = SpreadsheetReader.ParsePasted(["7000001  529.982.247-25  ANA LIMA  R$ 94,39", "Ativo"]);

        Assert.Equal(["7000001", "529.982.247-25", "ANA LIMA", "R$ 94,39", "Ativo"], rows[0]);
    }
}
