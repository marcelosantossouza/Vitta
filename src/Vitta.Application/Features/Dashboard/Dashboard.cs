using Mediator;
using Vitta.Application.Abstractions.Queries;
using Vitta.Application.Common;
using Vitta.Application.Features.Portfolio;
using Vitta.Application.Features.Prospects;
using Vitta.Domain.Prospects;

namespace Vitta.Application.Features.Dashboard;

public sealed record StageCountDto(ProspectStage Stage, int Count);

public sealed record InsurerSummaryDto(Guid InsurerId, string InsurerName, int Count, decimal MonthlyPremium);

public sealed record DashboardDto(
    int ActiveClients,
    int TotalClients,
    int ActivePolicies,
    decimal MonthlyPremiumTotal,
    int OpenProspects,
    IReadOnlyList<ProspectDto> FollowUpsDue,
    IReadOnlyList<StageCountDto> ProspectsByStage,
    IReadOnlyList<InsurerSummaryDto> PoliciesByInsurer,
    IReadOnlyList<PortfolioChangeDto> RecentExits,
    DateTimeOffset? LastImportAt,
    LastMonthCommissionDto? LastMonth = null);

/// <summary>
/// Comissao do ultimo mes carregado (mes de referencia mais recente das importacoes; varias seguradoras no mesmo mes
/// sao somadas) comparada com o mes anterior das mesmas seguradoras.
/// </summary>
/// <param name="Commission">Nulo quando as planilhas do mes nao trazem comissao nem %.</param>
/// <param name="ImportId">Preenchido quando o mes tem uma importacao so (o card leva direto para ela).</param>
public sealed record LastMonthCommissionDto(
    DateOnly Month, decimal? Commission, decimal Premium, int Policies, IReadOnlyList<string> Insurers, Guid? ImportId,
    DateOnly? PreviousMonth, decimal? PreviousCommission);

public sealed record GetDashboardQuery : IQuery<DashboardDto>;

public sealed class GetDashboardHandler(IDashboardQueries queries, TimeProvider time) : IQueryHandler<GetDashboardQuery, DashboardDto>
{
    public async ValueTask<DashboardDto> Handle(GetDashboardQuery q, CancellationToken ct) => await queries.GetAsync(time.Today(), ct);
}
