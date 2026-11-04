namespace Subscript.Domain.Common;

public abstract class SoftDeletableEntity : Entity
{
    public bool IsDeleted { get; protected set; }
    public DateTimeOffset? DeletedAt { get; protected set; }

    public virtual void SoftDelete(DateTimeOffset utcNow)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAt = utcNow;
        Touch(utcNow);
    }
}
