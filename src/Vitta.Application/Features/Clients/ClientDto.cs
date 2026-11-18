using Vitta.Domain.Clients;

namespace Vitta.Application.Features.Clients;

public sealed record ClientDto(Guid Id, string FullName, string Cpf, DateOnly? BirthDate, string? Email, string? Phone, string? Notes)
{
    public static ClientDto From(Client c) => new(c.Id, c.FullName, c.Cpf.Formatted, c.BirthDate, c.Email, c.Phone, c.Notes);
}
