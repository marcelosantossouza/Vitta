namespace Vitta.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; protected init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; protected set; }

    protected void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
