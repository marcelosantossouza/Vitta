using Microsoft.EntityFrameworkCore;
using Vitta.Persistence.Context;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Insurers;

namespace Vitta.Persistence.Repositories;

internal sealed class InsurerRepository(VittaDbContext db) : IInsurerRepository
{
    public async Task<IReadOnlyList<Insurer>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        await db.Insurers.AsNoTracking().Where(i => i.IsActive).OrderBy(i => i.Name).ToListAsync(cancellationToken);

    public Task<Insurer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Insurers.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
}
