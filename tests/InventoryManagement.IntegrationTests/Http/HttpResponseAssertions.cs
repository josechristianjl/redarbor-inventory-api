using System.Net;
using System.Text.Json;

namespace InventoryManagement.IntegrationTests.Http;

internal static class HttpResponseAssertions
{
    internal static async Task AssertNoContentAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    internal static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var problem = document.RootElement;
        Assert.Equal((int)status,
            problem.GetProperty("status").GetInt32());
        Assert.Equal(code,
            problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            problem.GetProperty("traceId").GetString()));
    }
}
