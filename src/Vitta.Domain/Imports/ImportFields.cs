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
/// <param name="Avoid">Palavras que, no cabecalho, indicam que a coluna NAO e deste campo (ex.: "produtor" no CPF do cliente).</param>
public sealed record ImportField(
    string Key, string Label, bool Required, bool Multiple, string Description, IReadOnlyList<string> Synonyms,
    ImportValueKind ValueKind = ImportValueKind.Text, IReadOnlyList<string>? Avoid = null)
{
    public IReadOnlyList<string> AvoidWords => Avoid ?? [];
}

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
        public const string City = "city";
        public const string Competence = "competence";
        public const string Installment = "installment";
        public const string CommissionAmount = "commissionAmount";
        public const string CommissionRate = "commissionRate";
        public const string Extras = "extras";
    }

    /// <summary>Palavras que identificam colunas do proprio corretor/produtor (nunca sao dados do cliente).</summary>
    private static readonly string[] BrokerWords = ["produtor", "corretor", "corretora", "agenciador", "verba", "angariacao"];

    public static class Prospects
    {
        public const string Name = "name";
        public const string Phone = "phone";
        public const string Email = "email";
        public const string Source = "source";
        public const string NextFollowUpOn = "nextFollowUpOn";
        public const string Notes = "notes";
        public const string Phone2 = "phone2";
        public const string Address = "address";
        public const string City = "city";
        public const string State = "state";
        public const string ZipCode = "zipCode";
        public const string BirthDate = "birthDate";
        public const string Occupation = "occupation";
        public const string EstimatedSalary = "estimatedSalary";
        public const string Discounts = "discounts";
        public const string Loans = "loans";
        public const string Cnpj = "cnpj";
        public const string LegalName = "legalName";
        public const string Activity = "activity";
        public const string CompanySize = "companySize";
        public const string OpenedOn = "openedOn";
    }

    private static readonly IReadOnlyList<ImportField> PortfolioFields =
    [
        new(Portfolio.PolicyNumber, "Nº da apólice", false, false,
            "Identificador único da apólice/proposta/certificado na seguradora (muda de linha para linha). Usado para comparar " +
            "um mês com o outro. Se não existir, o CPF (e o início da vigência) é usado no lugar. Um código igual em todas as " +
            "linhas (ex.: código do corretor) NÃO é número de apólice.",
            ["apolice", "n apolice", "no apolice", "numero apolice", "numero da apolice", "proposta", "n proposta", "numero da proposta", "certificado", "contrato"],
            ImportValueKind.Code, BrokerWords),
        new(Portfolio.HolderName, "Segurado", true, false,
            "Nome completo do segurado/titular da apólice.",
            ["segurado", "nome", "nome do segurado", "nome segurado", "cliente", "titular", "nome do cliente", "nome/razao social", "razao social", "nome / razao social"],
            ImportValueKind.Text, [.. BrokerWords, "produto", "plano"]),
        new(Portfolio.HolderCpf, "CPF", false, false,
            "CPF do segurado. Usado para ligar a linha ao cliente cadastrado (e para comparar os meses quando não há nº da apólice).",
            ["cpf", "cpf do segurado", "cpf segurado", "cpf/cnpj", "cpf/cnpj do cliente", "cpf do cliente", "documento"], ImportValueKind.Cpf, BrokerWords),
        new(Portfolio.ProductName, "Produto", false, false,
            "Nome do produto/plano/cobertura. Quando a proposta tem várias coberturas (uma linha cada), os nomes são juntados.",
            ["produto", "plano", "cobertura", "nome do produto", "descricao produto", "descricao do produto"], ImportValueKind.Text, ["codigo"]),
        new(Portfolio.MonthlyPremium, "Prêmio mensal", false, false,
            "Valor do prêmio (mensalidade) em reais. No extrato de comissões da MAG é o \"Valor base\". Coberturas da mesma proposta são somadas.",
            ["premio", "premio mensal", "valor", "valor mensal", "mensalidade", "premio total", "valor do premio", "valor base"], ImportValueKind.Money,
            [.. BrokerWords, "comissao", "estorno", "incentivo", "bonificacao"]),
        new(Portfolio.Status, "Situação", false, false,
            "Situação da apólice no mês (ativa, cancelada, suspensa, inadimplente...).",
            ["status", "situacao", "situacao da apolice", "status da apolice"], ImportValueKind.Status),
        new(Portfolio.StartDate, "Início da vigência", false, false,
            "Data de início/emissão da apólice. Sem nº da apólice, ajuda a diferenciar duas apólices do mesmo CPF.",
            ["inicio", "inicio da vigencia", "inicio vigencia", "vigencia", "data de inicio", "data inicio", "emissao", "data de emissao", "data adesao", "adesao"],
            ImportValueKind.Date),
        new(Portfolio.City, "Cidade", false, false,
            "Cidade do segurado.",
            ["cidade", "municipio", "cidade do segurado", "localidade"], ImportValueKind.Text, BrokerWords),
        new(Portfolio.Competence, "Competência", false, false,
            "Mês a que o arquivo se refere (ex.: 202610, 10/2026). Usado para conferir o mês da importação.",
            ["competencia", "competencia comissionada", "mes de competencia", "mes referencia", "referencia", "mes/ano"]),
        new(Portfolio.Installment, "Parcela", false, false,
            "Número da parcela paga (ex.: 26 = 26ª mensalidade).",
            ["parcela", "parcela comissionada", "n parcela", "numero da parcela"]),
        new(Portfolio.CommissionAmount, "Valor da comissão", false, false,
            "Comissão do corretor sobre a parcela, em reais. Coberturas da mesma proposta são somadas.",
            ["valor comissao", "comissao", "valor da comissao"], ImportValueKind.Money),
        new(Portfolio.CommissionRate, "% de comissão", false, false,
            "Percentual de comissão (ex.: 12%).",
            ["% comissao", "percentual comissao", "percentual de comissao", "taxa de comissao"]),
        new(Portfolio.Extras, "Outras informações", false, true,
            "Qualquer outra coluna útil sobre a apólice (código do produto, nº de inscrição, unidade de venda...). Pode receber várias colunas.",
            ["observacoes", "obs", "n inscricao", "inscricao", "up da venda"]),
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
        new(Prospects.Phone2, "Telefone 2", false, false,
            "Segundo telefone (fixo, recado, outro celular). Só use se já houver uma coluna para o telefone principal.",
            ["telefone 2", "telefone2", "celular 2", "fone 2", "telefone residencial", "telefone fixo", "tel 2", "telefone comercial"],
            ImportValueKind.Phone),
        new(Prospects.Address, "Endereço", false, false,
            "Logradouro com número e complemento (rua, avenida...). Bairro pode vir junto. Não é cidade nem CEP.",
            ["endereco", "logradouro", "rua", "endereco completo", "endereco residencial"], ImportValueKind.Text, ["eletronico", "email"]),
        new(Prospects.City, "Cidade", false, false,
            "Cidade/município onde a pessoa mora.",
            ["cidade", "municipio", "localidade", "cidade residencial"]),
        new(Prospects.State, "UF", false, false,
            "Estado (sigla de 2 letras, ex.: SP, MG).",
            ["uf", "estado", "sigla uf", "uf residencial"]),
        new(Prospects.ZipCode, "CEP", false, false,
            "CEP do endereço.",
            ["cep", "codigo postal"], ImportValueKind.Code),
        new(Prospects.BirthDate, "Data de nascimento", false, false,
            "Data de nascimento da pessoa (para calcular idade).",
            ["nascimento", "data de nascimento", "data nascimento", "dt nascimento", "dt nasc", "nasc"], ImportValueKind.Date),
        new(Prospects.Occupation, "Profissão / órgão", false, false,
            "Profissão, cargo, órgão ou empregador (ex.: professor, SEDUC, Prefeitura).",
            ["profissao", "cargo", "ocupacao", "orgao", "empregador", "empresa", "secretaria", "lotacao", "funcao"]),
        new(Prospects.EstimatedSalary, "Salário estimado", false, false,
            "Renda/salário bruto mensal estimado em reais (base para saber quanto a pessoa pode pagar).",
            ["salario", "salario bruto", "salario base", "renda", "renda mensal", "remuneracao", "vencimentos", "valor bruto", "bruto"],
            ImportValueKind.Money, ["liquido", "desconto", "emprestimo", "margem"]),
        new(Prospects.Discounts, "Descontos", false, false,
            "Total de descontos mensais em reais (impostos, previdência, contribuições).",
            ["descontos", "total descontos", "total de descontos", "desconto"], ImportValueKind.Money, ["emprestimo", "consignado"]),
        new(Prospects.Loans, "Empréstimos", false, false,
            "Valor mensal comprometido com empréstimos/consignados em reais.",
            ["emprestimos", "emprestimo", "consignado", "consignados", "parcela emprestimo", "valor emprestimo"], ImportValueKind.Money),
        new(Prospects.Cnpj, "CNPJ", false, false,
            "CNPJ da empresa (leads de empresas, ex.: seguro de vida em grupo). Não é CPF.",
            ["cnpj", "cnpj da empresa", "numero cnpj"], ImportValueKind.Code),
        new(Prospects.LegalName, "Razão social", false, false,
            "Nome oficial da empresa (razão social). Use 'Nome' para o nome fantasia/como é conhecida.",
            ["razao social", "nome empresarial", "razao"]),
        new(Prospects.Activity, "Atividade (CNAE)", false, false,
            "Atividade principal da empresa (código e/ou descrição do CNAE).",
            ["atividade", "atividade principal", "cnae", "cnae principal", "atividade (cnae)", "ramo", "segmento"]),
        new(Prospects.CompanySize, "Porte", false, false,
            "Porte da empresa: ME, EPP ou Demais.",
            ["porte", "porte da empresa", "tamanho"]),
        new(Prospects.OpenedOn, "Data de abertura", false, false,
            "Data de abertura/início de atividade da empresa.",
            ["data de abertura", "abertura", "inicio atividade", "data inicio atividade", "fundacao"], ImportValueKind.Date),
        new(Prospects.Notes, "Outras informações", false, true,
            "Qualquer outra coluna útil sem campo próprio (matrícula, idade, filhos, interesse...). Pode receber várias colunas; fica visível no prospect mas não é filtrável.",
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
