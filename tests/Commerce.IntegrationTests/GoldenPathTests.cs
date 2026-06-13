using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Commerce.IntegrationTests;

[Collection("commerce")]
public class GoldenPathTests(CommerceStackFixture fx)
{
    private static readonly Guid SeededProduct = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private async Task<string> RegisterAndLogin(HttpClient id, string email)
    {
        await id.PostAsJsonAsync("/auth/register", new { email, password = "Passw0rd!" });
        var resp = await id.PostAsJsonAsync("/auth/login", new { email, password = "Passw0rd!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<TokenDto>();
        return body!.accessToken;
    }
    private record TokenDto(string accessToken);

    [Fact]
    public async Task Login_returns_jwt()
    {
        var token = await RegisterAndLogin(fx.Identity.CreateClient(), "login@test.com");
        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Order_without_auth_is_401()
    {
        var resp = await fx.Order.CreateClient().PostAsJsonAsync("/orders",
            new { productId = SeededProduct, quantity = 1 });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Order_with_unknown_product_is_404()
    {
        var token = await RegisterAndLogin(fx.Identity.CreateClient(), "unknown@test.com");
        var client = fx.Order.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.PostAsJsonAsync("/orders",
            new { productId = Guid.NewGuid(), quantity = 1 });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Valid_order_is_created()
    {
        var token = await RegisterAndLogin(fx.Identity.CreateClient(), "order@test.com");
        var client = fx.Order.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.PostAsJsonAsync("/orders",
            new { productId = SeededProduct, quantity = 2 });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Order_flows_through_broker_to_notification()
    {
        var id = fx.Identity.CreateClient();
        var token = await RegisterAndLogin(id, "e2e@test.com");

        var order = fx.Order.CreateClient();
        order.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await order.PostAsJsonAsync("/orders", new { productId = SeededProduct, quantity = 1 }))
            .EnsureSuccessStatusCode();

        var notify = fx.Notification.CreateClient();
        notify.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Poll up to 10s for the async consume → Redis write.
        string body = "[]";
        for (var i = 0; i < 20; i++)
        {
            body = await (await notify.GetAsync("/notifications/me")).Content.ReadAsStringAsync();
            if (body.Contains("confirmed")) break;
            await Task.Delay(500);
        }
        body.Should().Contain("confirmed");
    }
}
