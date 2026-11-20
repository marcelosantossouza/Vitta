using Vitta.Domain.Imports;

namespace Vitta.Domain.UnitTests;

public class ImportTemplateTests
{
    [Fact]
    public void Create_RequiresRequiredFields()
    {
        var result = ImportTemplate.Create("MAG", ImportTemplateKind.Portfolio, null, null, 1, [new("policyNumber", "Apólice")], true, null);

        Assert.True(result.IsFailure);
        Assert.Contains("Segurado", result.Error.Message);
    }

    [Fact]
    public void Create_RejectsSecondColumnForSingleField_ButAllowsManyNotes()
    {
        var single = ImportTemplate.Create("X", ImportTemplateKind.Portfolio, null, null, 1,
            [new("policyNumber", "A"), new("holderName", "B"), new("holderName", "C")], false, null);
        var notes = ImportTemplate.Create("Y", ImportTemplateKind.Prospects, null, null, 1,
            [new("name", "Nome"), new("notes", "Profissão"), new("notes", "Filhos")], false, null);

        Assert.Equal("ImportTemplate.DuplicateField", single.Error.Code);
        Assert.Equal(3, notes.Value.Columns.Count);
    }

    [Fact]
    public void Prospects_IgnoreInsurer()
    {
        var t = ImportTemplate.Create("Y", ImportTemplateKind.Prospects, Guid.NewGuid(), " Plan1 ", 2, [new("name", "Nome")], false, null).Value;

        Assert.Null(t.InsurerId);
        Assert.Equal("Plan1", t.SheetName);
    }
}

public class ImportTemplateWithoutPolicyTests
{
    [Fact]
    public void Portfolio_AcceptsCpfInsteadOfPolicy_AndNoHeaderRow()
    {
        var ok = ImportTemplate.Create("MAG site", ImportTemplateKind.Portfolio, null, null, 0,
            [new("holderCpf", "Coluna 2"), new("holderName", "Coluna 3")], true, null);
        var neither = ImportTemplate.Create("MAG x", ImportTemplateKind.Portfolio, null, null, 0,
            [new("holderName", "Coluna 3")], true, null);

        Assert.True(ok.IsSuccess);
        Assert.Equal(0, ok.Value.HeaderRow);
        Assert.Contains("apólice (ou CPF)", neither.Error.Message);
    }
}
