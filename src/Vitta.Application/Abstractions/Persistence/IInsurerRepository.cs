using Vitta.Domain.Insurers;

namespace Vitta.Application.Abstractions.Persistence;

public interface IInsurerRepository
{
    Task<IReadOnlyList<Insurer>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<Insurer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
