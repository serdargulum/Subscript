namespace Subscript.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; protected set; }
    public DateTimeOffset UpdatedAt { get; protected set; }

    protected void Touch(DateTimeOffset utcNow)
    {
        UpdatedAt = utcNow;
    }

    protected void SetCreated(DateTimeOffset utcNow)
    {
        CreatedAt = utcNow;
        UpdatedAt = utcNow;
    }
}
