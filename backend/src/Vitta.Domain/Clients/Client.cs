using Vitta.Domain.Common;

namespace Vitta.Domain.Clients;

/// <summary>Cliente (segurado) da carteira da corretora.</summary>
public sealed class Client : AggregateRoot
{
    private Client() { } // EF

    private Client(string fullName, Cpf cpf, DateOnly? birthDate, string? email, string? phone)
    {
        FullName = fullName;
        Cpf = cpf;
        BirthDate = birthDate;
        Email = email;
        Phone = phone;
    }

    public string FullName { get; private set; } = null!;
    public Cpf Cpf { get; private set; } = null!;
    public DateOnly? BirthDate { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Notes { get; private set; }

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

        return new Client(fullName.Trim(), cpfResult.Value, birthDate, email?.Trim(), phone?.Trim());
    }

    public void UpdateContact(string? email, string? phone)
    {
        Email = email?.Trim();
        Phone = phone?.Trim();
        Touch();
    }

    public void SetNotes(string? notes)
    {
        Notes = notes;
        Touch();
    }
}

public static class ClientErrors
{
    public static readonly Error NameRequired = Error.Validation("Client.NameRequired", "Nome e obrigatorio.");
    public static readonly Error CpfAlreadyExists = Error.Conflict("Client.CpfAlreadyExists", "Ja existe um cliente com este CPF.");
    public static Error NotFound(Guid id) => Error.NotFound("Client.NotFound", $"Cliente {id} nao encontrado.");
}
