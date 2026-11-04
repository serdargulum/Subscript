namespace Subscript.Application.Common;

public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
