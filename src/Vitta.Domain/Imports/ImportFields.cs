namespace Vitta.Domain.Imports;

public enum ImportTemplateKind
{
    /// <summary>Planilha mensal de carteira enviada pela seguradora.</summary>
    Portfolio = 1,
    /// <summary>Lista de possiveis clientes (prospeccao).</summary>
    Prospects = 2,
}

/// <summary>Formato esperado dos valores: ajuda a reconhecer colunas sem cabecalho e a perceber mudanca de layout.</summary>
public enum ImportValueKind
{
    Text = 0,
    Cpf,
    Money,
    Date,
    Email,
    Phone,
    Status,
    Code,
}

/// <summary>Campo do Vitta que pode receber uma coluna da planilha.</summary>
/// <param name="Synonyms">Nomes de coluna conhecidos (sem acento, minusculos) - usados quando a IA nao esta disponivel.</param>
/// <param name="Multiple">Aceita varias colunas (ex.: observacoes juntam "Coluna: valor").</param>
public sealed record ImportField(
    string Key, string Label, bool Required, bool Multiple, string Description, IReadOnlyList<string> Synonyms,
    ImportValueKind ValueKind = ImportValueKind.Text);

/// <summary>Catalogo unico dos campos por tipo de importacao (fonte para API, IA e leitura).</summary>
public static class ImportFields
{
    public static class Portfolio
    {
        public const string PolicyNumber = "policyNumber";
        public const string HolderName = "holderName";
        public const string HolderCpf = "holderCpf";
        public const string ProductName = "productName";
        public const string MonthlyPremium = "monthlyPremium";
        public const string Status = "status";
        public const string StartDate = "startDate";
    }

    public static class Prospects
    {
        public const string Name = "name";
        public const string Phone = "phone";
        public const string Email = "email";
        public const string Source = "source";
        public const string NextFollowUpOn = "nextFollowUpOn";
        public const string Notes = "notes";
    }

    private static readonly IReadOnlyList<ImportField> PortfolioFields =
    [
        new(Portfolio.PolicyNumber, "Nº da apólice", false, false,
            "Identificador único da apólice/proposta/certificado na seguradora (muda de linha para linha). Usado para comparar " +
            "um mês com o outro. Se não existir, o CPF (e o início da vigência) é usado no lugar. Um código igual em todas as " +
            "linhas (ex.: código do corretor) NÃO é número de apólice.",
            ["apolice", "n apolice", "no apolice", "numero apolice", "numero da apolice", "proposta", "n proposta", "certificado", "contrato"],
            ImportValueKind.Code),
        new(Portfolio.HolderName, "Segurado", true, false,
            "Nome completo do segurado/titular da apólice.",
            ["segurado", "nome", "nome do segurado", "nome segurado", "cliente", "titular", "nome do cliente"]),
        new(Portfolio.HolderCpf, "CPF", false, false,
            "CPF do segurado. Usado para ligar a linha ao cliente cadastrado (e para comparar os meses quando não há nº da apólice).",
            ["cpf", "cpf do segurado", "cpf segurado", "cpf/cnpj", "documento"], ImportValueKind.Cpf),
        new(Portfolio.ProductName, "Produto", false, false,
            "Nome do produto/plano/cobertura contratado.",
            ["produto", "plano", "cobertura", "nome do produto"]),
        new(Portfolio.MonthlyPremium, "Prêmio mensal", false, false,
            "Valor do prêmio (mensalidade) em reais.",
            ["premio", "premio mensal", "valor", "valor mensal", "mensalidade", "premio total", "valor do premio"], ImportValueKind.Money),
        new(Portfolio.Status, "Situação", false, false,
            "Situação da apólice no mês (ativa, cancelada, suspensa, inadimplente...).",
            ["status", "situacao", "situacao da apolice", "status da apolice"], ImportValueKind.Status),
        new(Portfolio.StartDate, "Início da vigência", false, false,
            "Data de início/emissão da apólice. Sem nº da apólice, ajuda a diferenciar duas apólices do mesmo CPF.",
            ["inicio", "inicio da vigencia", "inicio vigencia", "vigencia", "data de inicio", "data inicio", "emissao", "data de emissao", "data adesao", "adesao"],
            ImportValueKind.Date),
    ];

    private static readonly IReadOnlyList<ImportField> ProspectFields =
    [
        new(Prospects.Name, "Nome", true, false,
            "Nome da pessoa (possível cliente).",
            ["nome", "nome completo", "cliente", "contato", "prospect", "lead"]),
        new(Prospects.Phone, "Telefone", false, false,
            "Telefone/celular/WhatsApp.",
            ["telefone", "celular", "whatsapp", "fone", "tel", "contato telefonico"], ImportValueKind.Phone),
        new(Prospects.Email, "E-mail", false, false,
            "Endereço de e-mail.",
            ["email", "e-mail", "e mail"], ImportValueKind.Email),
        new(Prospects.Source, "Origem", false, false,
            "De onde veio o contato (indicação, evento, Instagram, site...).",
            ["origem", "fonte", "indicacao", "indicado por", "canal"]),
        new(Prospects.NextFollowUpOn, "Próximo contato", false, false,
            "Data do próximo contato/retorno agendado.",
            ["proximo contato", "retorno", "data de retorno", "follow up", "follow-up", "agendamento"], ImportValueKind.Date),
        new(Prospects.Notes, "Observações", false, true,
            "Qualquer outra informação útil (profissão, idade, filhos, interesse...). Pode receber várias colunas.",
            ["observacoes", "observacao", "obs", "anotacoes", "comentarios", "notas"]),
    ];

    public static IReadOnlyList<ImportField> For(ImportTemplateKind kind) => kind switch
    {
        ImportTemplateKind.Portfolio => PortfolioFields,
        ImportTemplateKind.Prospects => ProspectFields,
        _ => [],
    };

    /// <summary>Campo pela chave, em qualquer tipo (as chaves sao unicas).</summary>
    public static ImportField? Find(string key) =>
        PortfolioFields.FirstOrDefault(f => f.Key == key) ?? ProspectFields.FirstOrDefault(f => f.Key == key);

    /// <summary>
    /// O que falta para o mapeamento ser utilizavel (rotulos para a mensagem). Alem dos campos obrigatorios, a carteira
    /// precisa de uma chave para comparar os meses: nº da apolice ou, na falta dele, o CPF.
    /// </summary>
    public static IReadOnlyList<string> MissingRequired(ImportTemplateKind kind, IEnumerable<string> mappedFields)
    {
        var mapped = mappedFields.ToHashSet();
        var missing = For(kind).Where(f => f.Required && !mapped.Contains(f.Key)).Select(f => f.Label).ToList();
        if (kind == ImportTemplateKind.Portfolio && !mapped.Contains(Portfolio.PolicyNumber) && !mapped.Contains(Portfolio.HolderCpf))
        {
            missing.Insert(0, "Nº da apólice (ou CPF)");
        }

        return missing;
    }
}
