using Vitta.Application.Features.Imports;
using Vitta.Application.Features.Prospects;
using Vitta.Domain.Imports;
using Vitta.Domain.Prospects;

namespace Vitta.Application.UnitTests;

public class ProspectManagementTests
{
    private static readonly DateOnly Today = new(2026, 11, 26);

    private static readonly string[][] Rows =
    [
        ["Nome", "Celular", "Cidade", "UF", "Salário Bruto", "Descontos", "Empréstimos", "Órgão", "Matrícula"],
        ["Ana Lima", "(11) 98888-1111", "Campinas", "sp", "R$ 6.500,00", "1.200,50", "800", "SEDUC", "123"],
        ["Bruno Reis", "(11) 97777-2222", "São Paulo", "SP", "4.000", "", "", "Prefeitura", "456"],
        ["Carla Souza", "", "Campinas", "SP", "texto", "", "", "", ""],
    ];

    private static ImportTemplate Template() => ImportTemplate.Create("Servidores", ImportTemplateKind.Prospects, null, null, 1,
        [
            new("name", "Nome"), new("phone", "Celular"), new("city", "Cidade"), new("state", "UF"), new("estimatedSalary", "Salário Bruto"),
            new("discounts", "Descontos"), new("loans", "Empréstimos"), new("occupation", "Órgão"), new("notes", "Matrícula"),
        ],
        createdByAi: true, aiNotes: null).Value;

    private static (ImportProspectsHandler Handler, FakeProspectRepository Prospects, FakeProspectImportRepository Imports, ImportTemplate Template)
        Setup(string[][] rows, FakeProspectRepository? prospects = null)
    {
        var template = Template();
        var templates = new FakeImportTemplateRepository();
        templates.Add(template);
        prospects ??= new FakeProspectRepository();
        var imports = new FakeProspectImportRepository();
        return (new ImportProspectsHandler(templates, prospects, imports, new FakeSpreadsheetReader(rows), TimeProvider.System, new FakeUnitOfWork()),
            prospects, imports, template);
    }

    [Fact]
    public async Task Import_ReadsProfileFields_AndLinksToBatch()
    {
        var (handler, prospects, imports, template) = Setup(Rows);

        var result = (await handler.Handle(new ImportProspectsCommand(template.Id, "lista.xlsx", Stream.Null), default)).Value;

        Assert.Equal(3, result.Created);
        var batch = Assert.Single(imports.Imports);
        Assert.Equal((batch.Id, 3, "Servidores"), (result.ImportId!.Value, batch.Created, batch.TemplateName));
        var ana = prospects.Prospects.Single(p => p.Name == "Ana Lima");
        Assert.Equal(("Campinas", "SP", 6500m, 1200.50m, 800m, "SEDUC"), (ana.City, ana.State, ana.EstimatedSalary!.Value, ana.Discounts!.Value, ana.Loans!.Value, ana.Occupation));
        Assert.Equal("Matrícula: 123", ana.Notes);
        Assert.Equal(batch.Id, ana.ImportId);
        Assert.Null(prospects.Prospects.Single(p => p.Name == "Carla Souza").EstimatedSalary); // "texto" no salario: fica vazio
    }

    [Fact]
    public async Task Import_Twice_SkipsByDefault_OrFillsOnlyEmptyFieldsWhenAsked()
    {
        var existing = Prospect.Create("Ana Lima", "11988881111", null, null).Value;
        existing.SetProfile(new ProspectProfile(City: "Valinhos"));
        var repo = new FakeProspectRepository();
        repo.Add(existing);

        var (skip, _, _, t1) = Setup(Rows, repo);
        var skipped = (await skip.Handle(new ImportProspectsCommand(t1.Id, "lista.xlsx", Stream.Null), default)).Value;
        Assert.Equal((2, 0, 1), (skipped.Created, skipped.Updated, skipped.Skipped));
        Assert.Null(existing.EstimatedSalary);

        var (update, _, _, t2) = Setup(Rows, repo);
        var updated = (await update.Handle(new ImportProspectsCommand(t2.Id, "lista.xlsx", Stream.Null, UpdateExisting: true), default)).Value;
        Assert.Equal(1, updated.Updated);
        Assert.Equal(6500m, existing.EstimatedSalary);
        Assert.Equal("Valinhos", existing.City); // o que ja existia nao e trocado
    }

    [Fact]
    public async Task DeleteImport_RemovesUntouched_KeepsWorkedOnes()
    {
        var (handler, prospects, imports, template) = Setup(Rows);
        var id = (await handler.Handle(new ImportProspectsCommand(template.Id, "lista.xlsx", Stream.Null), default)).Value.ImportId!.Value;
        prospects.Prospects.Single(p => p.Name == "Bruno Reis").MoveTo(ProspectStage.Contacted);

        var result = await new DeleteProspectImportHandler(imports, prospects, new FakeUnitOfWork()).Handle(new DeleteProspectImportCommand(id), default);

        Assert.Equal((2, 1), (result.Value.Removed, result.Value.Kept));
        var bruno = Assert.Single(prospects.Prospects);
        Assert.Null(bruno.ImportId);
        Assert.Empty(imports.Imports);
    }

    [Fact]
    public void Filter_BySalaryCityStateFollowUpAndPhone()
    {
        Prospect P(string name, string? city, string? uf, decimal? salary, DateOnly? followUp = null, string? phone = "11999990000")
        {
            var p = Prospect.Create(name, phone, null, "Lista", null, followUp).Value;
            p.SetProfile(new ProspectProfile(City: city, State: uf, EstimatedSalary: salary));
            return p;
        }

        var list = new[]
        {
            P("Ana", "Campinas", "SP", 6500m, Today.AddDays(-1)),
            P("Bruno", "campinas", "sp", 3000m, Today),
            P("Carla", "Recife", "PE", null, null, phone: null),
            P("Davi", "Campinas", "SP", 9000m, Today.AddDays(3)),
        };

        string[] Names(ProspectFilter f, ProspectSort s = ProspectSort.Name) =>
            ProspectFiltering.Sort(ProspectFiltering.Apply(list, f, Today), s).Select(p => p.Name).ToArray();

        Assert.Equal(["Ana", "Bruno", "Davi"], Names(new ProspectFilter(City: "CAMPINAS")));      // ignora maiusculas
        Assert.Equal(["Davi", "Ana"], Names(new ProspectFilter(SalaryMin: 5000m), ProspectSort.SalaryDesc));
        Assert.Equal(["Bruno"], Names(new ProspectFilter(SalaryMax: 4000m)));                      // sem salario nao entra
        Assert.Equal(["Ana"], Names(new ProspectFilter(FollowUp: FollowUpFilter.Overdue)));
        Assert.Equal(["Bruno", "Davi"], Names(new ProspectFilter(FollowUp: FollowUpFilter.Next7Days)));
        Assert.Equal(["Carla"], Names(new ProspectFilter(HasPhone: false)));
        Assert.Equal(["Carla"], Names(new ProspectFilter(State: "pe")));
    }

    [Fact]
    public void Insights_CountsFunnelCitiesAndSalaryBands()
    {
        var a = Prospect.Create("Ana", null, null, null).Value; a.SetProfile(new ProspectProfile(City: "Campinas", EstimatedSalary: 2500m));
        var b = Prospect.Create("Bia", null, null, null).Value; b.SetProfile(new ProspectProfile(City: "campinas", EstimatedSalary: 9000m)); b.MoveTo(ProspectStage.Won);
        var c = Prospect.Create("Cid", null, null, null).Value; c.MoveTo(ProspectStage.Lost);

        var i = ProspectFiltering.Insights([a, b, c], Today, DateTimeOffset.UtcNow);

        Assert.Equal((3, 1, 1, 1), (i.Total, i.Open, i.Won, i.Lost));
        Assert.Equal(50m, i.ConversionRate);
        Assert.Equal(5750m, i.AverageSalary);
        Assert.Equal(new LabelCount("Campinas", 2), i.TopCities[0]);
        Assert.Equal(1, i.SalaryBands.Single(x => x.Label == "Até R$ 3 mil").Count);
        Assert.Equal(1, i.SalaryBands.Single(x => x.Label == "R$ 8 a 12 mil").Count);
        Assert.Equal(1, i.SalaryBands.Single(x => x.Label == "Sem salário").Count);
    }
}
