using Vitta.Domain.Common;

namespace Vitta.Domain.Clients;

/// <summary>Cliente (segurado) da carteira da corretora.</summary>
public sealed class Client : AggregateRoot
{
    private Client() { } // EF

    private Client(string fullName, Cpf cpf, DateOnly? birthDate, string? email, string? phone)
    {
        FullName = fullName;
        SearchName = TextNormalizer.ForSearch(fullName);
        Cpf = cpf;
        BirthDate = birthDate;
        Email = email;
        Phone = phone;
    }

    public string FullName { get; private set; } = null!;
    /// <summary>Nome normalizado (minusculo, sem acento) para busca portavel entre bancos.</summary>
    public string SearchName { get; private set; } = null!;
    public Cpf Cpf { get; private set; } = null!;
    public DateOnly? BirthDate { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>Grupo de indicacao que trouxe este segurado (pessoa, empresa, sindicato ou codigo).</summary>
    public Guid? ReferralGroupId { get; private set; }

    public static Result<Client> Create(string fullName, string cpf, DateOnly? birthDate, string? email, string? phone)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return ClientErrors.NameRequired;
        }

        var cpfResult = Cpf.Create(cpf);
        if (cpfResult.IsFailure)
        {
            return cpfResult.Error;
        }

        return new Client(fullName.Trim(), cpfResult.Value, birthDate, Clean(email), Clean(phone));
    }

    public static Result<Client> Create(string fullName, string cpf, DateOnly? birthDate, string? email, string? phone, string? notes)
    {
        var result = Create(fullName, cpf, birthDate, email, phone);
        if (result.IsSuccess)
        {
            result.Value.Notes = Clean(notes);
        }

        return result;
    }

    /// <summary>Atualiza os dados cadastrais. O CPF nao muda (identifica o cliente nas seguradoras).</summary>
    public void AssignReferral(Guid? referralGroupId)
    {
        if (ReferralGroupId == referralGroupId)
        {
            return;
        }

        ReferralGroupId = referralGroupId;
        Touch();
    }

    public Result Update(string fullName, DateOnly? birthDate, string? email, string? phone, string? notes)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return ClientErrors.NameRequired;
        }

        FullName = fullName.Trim();
        SearchName = TextNormalizer.ForSearch(FullName);
        BirthDate = birthDate;
        Email = Clean(email);
        Phone = Clean(phone);
        Notes = Clean(notes);
        Touch();
        return Result.Success();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class ClientErrors
{
    public static readonly Error NameRequired = Error.Validation("Client.NameRequired", "Nome é obrigatório.");
    public static readonly Error CpfAlreadyExists = Error.Conflict("Client.CpfAlreadyExists", "Já existe um cliente com este CPF.");
    public static readonly Error HasPolicies = Error.Conflict("Client.HasPolicies", "Este cliente possui apólices e não pode ser excluído.");
    public static Error NotFound(Guid id) => Error.NotFound("Client.NotFound", "Cliente não encontrado.");
}
