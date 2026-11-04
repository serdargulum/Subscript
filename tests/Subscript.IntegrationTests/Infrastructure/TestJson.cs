using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Subscript.IntegrationTests.Infrastructure;

/// <summary>
/// The API serializes enums as strings, so the test client must use matching options.
/// </summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static Task<HttpResponseMessage> PostJsonAsync<T>(
        this HttpClient client,
        string path,
        T value)
        => client.PostAsJsonAsync(path, value, Options);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(
        this HttpClient client,
        string path,
        T value)
        => client.PutAsJsonAsync(path, value, Options);

    public static async Task<T?> ReadJsonAsync<T>(this HttpContent content)
        => await content.ReadFromJsonAsync<T>(Options);
}
