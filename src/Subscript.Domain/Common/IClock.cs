namespace Subscript.Domain.Common;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
