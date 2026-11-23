using Microsoft.Extensions.Logging.Abstractions;
using Vitta.Application.Abstractions.Ai;
using Vitta.Application.Abstractions.Imports;
using Vitta.Application.Features.Imports;
using Vitta.Domain.Imports;
using Vitta.Domain.Prospects;

namespace Vitta.Application.UnitTests;

public class SpreadsheetMapperTests
{
    private static readonly string[][] MagLike =
    [
        ["MONGERAL AEGON - Relatório de Carteira", "", "", ""],
        ["Corretor: 12345", "", "", ""],
        ["Nº Proposta", "Nome do Segurado", "CPF/CNPJ", "Situação"],
        ["001", "Ana Lima", "529.982.247-25", "ATIVO"],
        ["", "", "", ""],
        ["002", "Bruno Reis", "", "CANCELADO"],
    ];

    [Fact]
    public void DetectsHeaderBelowTitleRows_AndAutoMaps()
    {
        var header = SpreadsheetMapper.DetectHeaderRow(MagLike, ImportTemplateKind.Portfolio);
        var columns = SpreadsheetMapper.AutoMap(MagLike[header - 1], ImportTemplateKind.Portfolio);

        Assert.Equal(3, header);
        Assert.Contains(new ColumnMapping("policyNumber", "Nº Proposta"), columns);
        Assert.Contains(new ColumnMapping("holderName", "Nome do Segurado"), columns);
        Assert.Contains(new ColumnMapping("holderCpf", "CPF/CNPJ"), columns);
        Assert.Contains(new ColumnMapping("status", "Situação"), columns);
    }

    [Fact]
    public void Apply_SkipsEmptyRows_AndMatchesHeadersIgnoringAccentsAndCase()
    {
        var data = new SpreadsheetData([], null, MagLike);
        var result = SpreadsheetMapper.Apply(data, 3, [new("policyNumber", "nº proposta"), new("holderName", "NOME DO SEGURADO")], "MAG");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("Bruno Reis", result.Value[1].Get("holderName"));
        Assert.Equal(6, result.Value[1].RowNumber);
    }

    [Fact]
    public void Apply_MissingColumn_ExplainsLayoutChange()
    {
        var result = SpreadsheetMapper.Apply(new SpreadsheetData([], null, MagLike), 3, [new("policyNumber", "Apólice")], "Carteira MAG");

        Assert.True(result.IsFailure);
        Assert.Contains("\"Apólice\"", result.Error.Message);
        Assert.Contains("Carteira MAG", result.Error.Message);
    }

    [Theory]
    [InlineData("15/03/2026", 2026, 3, 15)]
    [InlineData("2026-03-15", 2026, 3, 15)]
    [InlineData("5/3/26", 2026, 3, 5)]
    public void ParseDate_AcceptsBrazilianAndIso(string input, int y, int m, int d) =>
        Assert.Equal(new DateOnly(y, m, d), SpreadsheetMapper.ParseDate(input));

    [Theory]
    [InlineData("R$ 1.234,56", 1234.56)]
    [InlineData("120.5", 120.5)]
    [InlineData("89,90", 89.90)]
    public void ParseMoney_HandlesFormats(string input, double expected) =>
        Assert.Equal((decimal)expected, SpreadsheetMapper.ParseMoney(input));
}

public class AnalyzeSpreadsheetHandlerTests
{
    private static readonly string[][] Rows =
    [
        ["Lista de contatos 2026", "", ""],
        ["Nome", "Celular", "E-mail"],
        ["Rafael Cunha", "(21) 98888-1111", "rafael@exemplo.com"],
    ];

    [Fact]
    public async Task UsesAiSuggestion_ResolvingColumnNumbersToRealHeaders_AndMasksPersonalData()
    {
        var ai = new FakeAiService(_ => new SpreadsheetSuggestion(2,
            [new("name", 1), new("phone", 2), new("email", 3), new("source", 7), new("name", 2)], "Contatos", "ok", "high", "ai"));
        var handler = new AnalyzeSpreadsheetHandler(new FakeSpreadsheetReader(Rows), ai, NullLogger<AnalyzeSpreadsheetHandler>.Instance);

        var result = await handler.Handle(new AnalyzeSpreadsheetCommand(ImportTemplateKind.Prospects, "contatos.xlsx", Stream.Null, null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("ai", result.Value.AnalyzedBy);
        Assert.Equal([new("name", "Nome"), new("phone", "Celular"), new("email", "E-mail")], result.Value.Columns);
        var sent = ai.LastRequest!.Rows[2];
        Assert.Equal(["Rafael Cunha", "(99) 99999-9999", "email@exemplo.com"], sent);
        Assert.Equal("(21) 98888-1111", result.Value.Rows[2][1]); // a pre-visualizacao continua com os dados reais
    }

    [Fact]
    public async Task FallsBackToHeuristic_WhenAiIsUnavailable()
    {
        var handler = new AnalyzeSpreadsheetHandler(new FakeSpreadsheetReader(Rows), new FakeAiService(), NullLogger<AnalyzeSpreadsheetHandler>.Instance);

        var result = await handler.Handle(new AnalyzeSpreadsheetCommand(ImportTemplateKind.Prospects, "contatos.csv", Stream.Null, null), default);

        Assert.Equal("heuristic", result.Value.AnalyzedBy);
        Assert.Equal(2, result.Value.HeaderRow);
        Assert.Contains(new ColumnMapping("phone", "Celular"), result.Value.Columns);
        Assert.Equal("Prospects – contatos", result.Value.SuggestedName);
    }
}

public class ImportProspectsHandlerTests
{
    [Fact]
    public async Task CreatesNewProspects_SkipsDuplicatesAndRowsWithoutName()
    {
        string[][] rows =
        [
            ["Nome", "Telefone", "Profissão", "Filhos", "Retorno"],
            ["Rafael Cunha", "(21) 98888-1111", "Médico", "2", "10/12/2026"],
            ["Rafael C.", "21 988881111", "", "", ""],   // mesmo telefone
            ["", "(21) 97777-0000", "", "", ""],          // sem nome
            ["Sabrina Teles", "", "Advogada", "", "amanhã"],
            ["Thiago Mattos", "(11) 95555-2222", "", "", ""], // ja existe
        ];
        var template = ImportTemplate.Create("Contatos", ImportTemplateKind.Prospects, null, null, 1,
            [new("name", "Nome"), new("phone", "Telefone"), new("notes", "Profissão"), new("notes", "Filhos"), new("nextFollowUpOn", "Retorno")],
            createdByAi: true, aiNotes: null).Value;
        var templates = new FakeImportTemplateRepository();
        templates.Add(template);
        var prospects = new FakeProspectRepository();
        prospects.Add(Prospect.Create("Thiago Mattos", "11955552222", null, null).Value);
        var uow = new FakeUnitOfWork();
        var handler = new ImportProspectsHandler(templates, prospects, new FakeSpreadsheetReader(rows), TimeProvider.System, uow);

        var result = await handler.Handle(new ImportProspectsCommand(template.Id, "contatos.xlsx", Stream.Null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal((5, 2, 3), (result.Value.TotalRows, result.Value.Created, result.Value.Skipped));
        var rafael = prospects.Prospects.Single(p => p.Name == "Rafael Cunha");
        Assert.Equal(new DateOnly(2026, 12, 10), rafael.NextFollowUpOn);
        Assert.Equal("Profissão: Médico\nFilhos: 2", rafael.Notes);
        Assert.Contains("Próximo contato: amanhã", prospects.Prospects.Single(p => p.Name == "Sabrina Teles").Notes);
        Assert.All(prospects.Prospects, p => Assert.Equal(ProspectStage.New, p.Stage));
        Assert.NotNull(template.LastUsedAt);
        Assert.Equal(1, uow.SaveCount);
    }
}

/// <summary>Tabela copiada do site da seguradora: sem cabecalho, sem nº de apolice (dados ficticios).</summary>
public class PastedTableTests
{
    private static readonly string[][] Pasted =
    [
        ["7000001", "529.982.247-25", "ANA MARIA DE SOUZA LIMA", "RIO DE JANEIRO", "22/05/2017", "R$ 1.234,56", "Ativo"],
        ["7000001", "111.444.777-35", "BRUNO DOS REIS PEREIRA", "NITERÓI", "01/08/2024", "R$ 269,31", "Ativo"],
        ["7000001", "390.533.447-05", "CARLA MENDES DA SILVA", "RIO DE JANEIRO", "10/12/2008", "R$ 94,39", "Cancelado"],
    ];

    [Fact]
    public void DetectsNoHeader_AndMapsColumnsByContent_IgnoringConstantCode()
    {
        var header = SpreadsheetMapper.DetectHeaderRow(Pasted, ImportTemplateKind.Portfolio);
        var columns = SpreadsheetMapper.AutoMapByContent(Pasted, ImportTemplateKind.Portfolio);

        Assert.Equal(0, header);
        Assert.Equal(
            [
                new ColumnMapping("holderCpf", "Coluna 2"), new ColumnMapping("holderName", "Coluna 3"),
                new ColumnMapping("startDate", "Coluna 5"), new ColumnMapping("monthlyPremium", "Coluna 6"),
                new ColumnMapping("status", "Coluna 7"),
            ],
            columns);
    }

    [Fact]
    public void ImportsByPosition_UsingCpfAndStartDateAsKey()
    {
        var data = new SpreadsheetData([], null, Pasted);
        var mapped = SpreadsheetMapper.Apply(data, 0, SpreadsheetMapper.AutoMapByContent(Pasted, ImportTemplateKind.Portfolio), "MAG site");

        var rows = Vitta.Application.Features.Portfolio.ImportPortfolioHandler.ToPortfolioRows(mapped.Value);

        Assert.Equal(3, rows.Count);
        Assert.Equal("CPF 529.982.247-25 · 22/05/2017", rows[0].DisplayId);
        Assert.Null(rows[0].PolicyNumber);
        Assert.Equal("ANA MARIA DE SOUZA LIMA", rows[0].HolderName);
        Assert.Equal("52998224725", rows[0].HolderCpf);
        Assert.Equal(1234.56m, rows[0].MonthlyPremium);
        Assert.Equal("Cancelado", rows[2].Status);
    }

    [Fact]
    public void Headerless_ColumnsInAnotherOrder_ExplainsLayoutChange()
    {
        // O site passou a mostrar a cidade antes do nome: a "Coluna 2" deixou de ter CPF.
        var shifted = Pasted.Select(r => new[] { r[0], r[3], r[1], r[2], r[4], r[5], r[6] }).ToArray();
        var result = SpreadsheetMapper.Apply(new SpreadsheetData([], null, shifted), 0,
            [new("holderCpf", "Coluna 2"), new("holderName", "Coluna 3")], "MAG site");

        Assert.True(result.IsFailure);
        Assert.Equal("ImportTemplate.LayoutChanged", result.Error.Code);
        Assert.Contains("Coluna 2", result.Error.Message);
    }
}

/// <summary>Extrato de comissoes da MAG (CSV): uma linha por cobertura, dados ficticios.</summary>
public class CommissionStatementTests
{
    private static readonly string[] Header =
    [
        "CPF/CNPJ do Produtor", "Código do produtor", "Tipo de cliente", "Nome/Razão social", "CPF/CNPJ do cliente", "Proposta",
        "UP da venda", "Nº Inscrição", "Descrição Produto", "Código do produto", "Valor base", "Parcela comissionada",
        "Competência comissionada", "Parcela faturada", "Competência faturada", "Valor Angariação", "Valor Comissão", "%  Comissão",
        "Código da Verba", "Nome da Verba",
    ];

    private static string[] Row(string name, string cpf, string proposal, string product, string value, string commission) =>
        ["11222333000181", "7000001", "INDIVIDUAL", name, cpf, proposal, "F01 - RIO DE JANEIRO", proposal + "1", product, "1546", value,
         "26", "202610", "26", "202609", "0", commission, "15%", "18", "CARTEIRA INDIVIDUAL"];

    private static readonly string[][] Statement =
    [
        Header,
        Row("ANA MARIA LIMA", "5299822472", "900000001", "CAPITAL SEGURADO POR MORTE", "1643.68", "164.37"),
        Row("BRUNO REIS", "11144477735", "900000002", "VIDA INTEIRA", "207.19", "31.08"),
        Row("BRUNO REIS", "11144477735", "900000002", "SAF ASSISTENCIA FUNERAL", "61.1", "9.16"),
    ];

    [Fact]
    public void AutoMap_IgnoresBrokerColumns_AndFindsClientData()
    {
        var columns = SpreadsheetMapper.AutoMap(Header, ImportTemplateKind.Portfolio);

        Assert.Contains(new ColumnMapping("holderName", "Nome/Razão social"), columns);
        Assert.Contains(new ColumnMapping("holderCpf", "CPF/CNPJ do cliente"), columns);
        Assert.Contains(new ColumnMapping("policyNumber", "Proposta"), columns);
        Assert.Contains(new ColumnMapping("productName", "Descrição Produto"), columns);
        Assert.Contains(new ColumnMapping("monthlyPremium", "Valor base"), columns);
        Assert.Contains(new ColumnMapping("commissionAmount", "Valor Comissão"), columns);
        Assert.Contains(new ColumnMapping("commissionRate", "%  Comissão"), columns);
        Assert.Contains(new ColumnMapping("competence", "Competência comissionada"), columns);
        Assert.Contains(new ColumnMapping("installment", "Parcela comissionada"), columns);
        Assert.DoesNotContain(columns, c => c.Column.Contains("Produtor") || c.Column == "Nome da Verba");
    }

    [Fact]
    public void CoveragesOfTheSameProposal_AreMergedIntoOnePolicy()
    {
        var columns = SpreadsheetMapper.AutoMap(Header, ImportTemplateKind.Portfolio);
        var mapped = SpreadsheetMapper.Apply(new SpreadsheetData([], null, Statement), 1, columns, "MAG comissões");

        var rows = Vitta.Application.Features.Portfolio.ImportPortfolioHandler.ToPortfolioRows(mapped.Value);

        Assert.Equal(2, rows.Count);
        var bruno = rows.Single(r => r.HolderName == "BRUNO REIS");
        Assert.Equal("900000002", bruno.PolicyNumber);
        Assert.Equal(268.29m, bruno.MonthlyPremium);
        Assert.Equal(40.24m, bruno.CommissionAmount);
        Assert.Equal(15m, bruno.CommissionRate);
        Assert.Equal("VIDA INTEIRA + SAF ASSISTENCIA FUNERAL", bruno.ProductName);
        Assert.Equal(2, bruno.Coverages);
        Assert.Equal(26, bruno.Installment);
        Assert.Equal(new DateOnly(2026, 10, 1), bruno.Competence);
        Assert.Equal("05299822472", rows.Single(r => r.HolderName == "ANA MARIA LIMA").HolderCpf); // zero a esquerda que o Excel tirou
    }

    [Theory]
    [InlineData("202610", 2026, 10)]
    [InlineData("10/2026", 2026, 10)]
    [InlineData("2026-10", 2026, 10)]
    [InlineData("15/10/2026", 2026, 10)]
    public void ParseCompetence_AcceptsCommonFormats(string input, int year, int month) =>
        Assert.Equal(new DateOnly(year, month, 1), Vitta.Application.Features.Portfolio.ImportPortfolioHandler.ParseCompetence(input));
}
