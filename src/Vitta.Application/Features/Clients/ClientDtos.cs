using Vitta.Application.Features.Policies;

namespace Vitta.Application.Features.Clients;

/// <param name="MonthlyCommission">Soma da comissao mensal das apolices ativas que tem percentual; nulo quando nenhuma tem.</param>
/// <param name="PoliciesWithoutRate">Apolices ativas ainda sem percentual de comissao (o total de comissao fica incompleto).</param>
public sealed record ClientListItemDto(
    Guid Id, string FullName, string Cpf, string? Phone, string? Email, int ActivePolicyCount, decimal MonthlyPremium,
    decimal? MonthlyCommission, int PoliciesWithoutRate, Guid? ReferralGroupId = null, string? ReferralGroupName = null);

public sealed record ClientDetailDto(
    Guid Id, string FullName, string Cpf, DateOnly? BirthDate, string? Email, string? Phone, string? Notes,
    DateTimeOffset CreatedAt, IReadOnlyList<PolicyDto> Policies, Guid? ReferralGroupId = null, string? ReferralGroupName = null);
