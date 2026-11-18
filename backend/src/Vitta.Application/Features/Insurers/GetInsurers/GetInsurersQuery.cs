using Mediator;
using Vitta.Application.Abstractions.Persistence;

namespace Vitta.Application.Features.Insurers.GetInsurers;

public sealed record InsurerDto(Guid Id, string Code, string Name);

public sealed record GetInsurersQuery : IQuery<IReadOnlyList<InsurerDto>>;

public sealed class GetInsurersHandler(IInsurerRepository insurers) : IQueryHandler<GetInsurersQuery, IReadOnlyList<InsurerDto>>
{
    public async ValueTask<IReadOnlyList<InsurerDto>> Handle(GetInsurersQuery query, CancellationToken cancellationToken) =>
        (await insurers.ListActiveAsync(cancellationToken)).Select(i => new InsurerDto(i.Id, i.Code, i.Name)).ToList();
}
