using Microsoft.AspNetCore.Authentication;

namespace Subscript.Api.Authentication;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    public string ApiKey { get; set; } = string.Empty;
}
