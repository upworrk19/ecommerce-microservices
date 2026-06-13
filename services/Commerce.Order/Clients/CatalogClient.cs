using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Commerce.Order.Clients;

public record ProductDto(Guid Id, string Name, string Description, decimal Price, int Stock);

public class CatalogClient(HttpClient http)
{
    /// <summary>Validates a product exists by calling Catalog, forwarding the caller's JWT.</summary>
    public async Task<ProductDto?> GetProductAsync(Guid id, string bearerToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/products/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        var resp = await http.SendAsync(request, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ProductDto>(cancellationToken: ct);
    }
}
