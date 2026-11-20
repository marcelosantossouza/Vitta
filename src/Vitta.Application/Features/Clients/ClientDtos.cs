using Vitta.Application.Features.Policies;

namespace Vitta.Application.Features.Clients;

public sealed record ClientListItemDto(Guid Id, string FullName, string Cpf, string? Phone, string? Email, int ActivePolicyCount, decimal MonthlyPremium);

public sealed record ClientDetailDto(
    Guid Id, string FullName, string Cpf, DateOnly? BirthDate, string? Email, string? Phone, string? Notes,
    DateTimeOffset CreatedAt, IReadOnlyList<PolicyDto> Policies);
