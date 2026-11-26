using Microsoft.EntityFrameworkCore;
using Vitta.Application.Features.Contacts;
using Vitta.Persistence.Context;

namespace Vitta.Persistence.Queries;

internal sealed class ContactQueries(VittaDbContext db) : IContactQueries
{
    public async Task<IReadOnlyList<ContactLogDto>> ListAsync(Guid? prospectId, Guid? clientId, CancellationToken ct = default)
    {
        var list = await db.ContactLogs.AsNoTracking()
            .Where(x => (prospectId != null && x.ProspectId == prospectId) || (clientId != null && x.ClientId == clientId))
            .ToListAsync(ct);
        return list.OrderByDescending(x => x.OccurredAt)
            .Select(x => new ContactLogDto(x.Id, x.OccurredAt, x.Channel, x.Summary, x.UserName, x.UserId))
            .ToList();
    }
}
