using Commerce.Order.Clients;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

namespace Commerce.IntegrationTests;

/// <summary>
/// Boots real MSSQL, two Postgres, RabbitMQ and Redis via Testcontainers, then
/// hosts the four services in-process with WebApplicationFactory pointed at them.
/// Proves the wiring end-to-end against real infrastructure.
///
/// Adaptations from the plan's best-effort fixture:
///  * RabbitMQ: services read a full <c>RabbitMq:Uri</c> (= the container's
///    connection string, which carries the mapped host port + creds) rather than
///    host/user/pass — Testcontainers maps the broker to a random host port.
///  * Order -> Catalog: the typed CatalogClient is replaced (in ConfigureTestServices,
///    which runs AFTER the app's own registrations) with one bound to the in-memory
///    Catalog test server's HttpClient, so Order reaches Catalog without a real socket.
///  * The four marker Program classes live in their service namespaces so
///    WebApplicationFactory&lt;T&gt; can disambiguate the entry-point assembly.
/// </summary>
public class CommerceStackFixture : IAsyncLifetime
{
    public MsSqlContainer Mssql { get; } =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    public PostgreSqlContainer CatalogDb { get; } =
        new PostgreSqlBuilder("postgres:16").WithDatabase("catalog").Build();
    public PostgreSqlContainer OrdersDb { get; } =
        new PostgreSqlBuilder("postgres:16").WithDatabase("orders").Build();
    public RabbitMqContainer Rabbit { get; } =
        new RabbitMqBuilder("rabbitmq:3-management").Build();
    public RedisContainer Redis { get; } =
        new RedisBuilder("redis:7").Build();

    public WebApplicationFactory<Commerce.Identity.Program> Identity = default!;
    public WebApplicationFactory<Commerce.Catalog.Program> Catalog = default!;
    public WebApplicationFactory<Commerce.Order.Program> Order = default!;
    public WebApplicationFactory<Commerce.Notification.Program> Notification = default!;

    private const string Jwt = "dev-only-super-secret-key-change-me-32bytes!";

    public async Task InitializeAsync()
    {
        // Start infra first so the host builders can migrate / connect on startup.
        await Task.WhenAll(Mssql.StartAsync(), CatalogDb.StartAsync(),
            OrdersDb.StartAsync(), Rabbit.StartAsync(), Redis.StartAsync());

        Identity = Build<Commerce.Identity.Program>(new()
        {
            ["ConnectionStrings:Default"] = Mssql.GetConnectionString(),
            ["Jwt:Secret"] = Jwt, ["Jwt:Issuer"] = "commerce", ["Jwt:Audience"] = "commerce",
        });

        Catalog = Build<Commerce.Catalog.Program>(new()
        {
            ["ConnectionStrings:Default"] = CatalogDb.GetConnectionString(),
            ["Jwt:Secret"] = Jwt, ["Jwt:Issuer"] = "commerce", ["Jwt:Audience"] = "commerce",
        });

        // Force Catalog to build + migrate, then hand Order an HttpClient that talks
        // to Catalog's in-memory server.
        var catalogClient = Catalog.CreateClient();

        Order = Build<Commerce.Order.Program>(new()
        {
            ["ConnectionStrings:Default"] = OrdersDb.GetConnectionString(),
            ["Services:Catalog"] = "http://catalog.local",   // placeholder; CatalogClient is replaced below
            ["RabbitMq:Uri"] = Rabbit.GetConnectionString(),
            ["Jwt:Secret"] = Jwt, ["Jwt:Issuer"] = "commerce", ["Jwt:Audience"] = "commerce",
        }, catalogClient);

        Notification = Build<Commerce.Notification.Program>(new()
        {
            ["Redis:Connection"] = Redis.GetConnectionString(),
            ["RabbitMq:Uri"] = Rabbit.GetConnectionString(),
            ["Jwt:Secret"] = Jwt, ["Jwt:Issuer"] = "commerce", ["Jwt:Audience"] = "commerce",
        });

        // Force hosts to start (runs migrations + starts the MassTransit bus/consumer).
        _ = Identity.CreateClient();
        _ = Order.CreateClient();
        _ = Notification.CreateClient();
    }

    private WebApplicationFactory<T> Build<T>(
        Dictionary<string, string?> settings, HttpClient? injectedCatalog = null) where T : class
        => new WebApplicationFactory<T>().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(settings));

            // ConfigureTestServices runs AFTER the app's own ConfigureServices, so this
            // override wins: drop the HttpClient-backed CatalogClient and bind one to
            // the Catalog test server's HttpClient instead.
            if (injectedCatalog is not null)
                b.ConfigureTestServices(s =>
                {
                    s.RemoveAll<CatalogClient>();
                    s.AddSingleton(new CatalogClient(injectedCatalog));
                });
        });

    public async Task DisposeAsync()
    {
        if (Identity is not null) await Identity.DisposeAsync();
        if (Catalog is not null) await Catalog.DisposeAsync();
        if (Order is not null) await Order.DisposeAsync();
        if (Notification is not null) await Notification.DisposeAsync();
        await Task.WhenAll(Mssql.DisposeAsync().AsTask(), CatalogDb.DisposeAsync().AsTask(),
            OrdersDb.DisposeAsync().AsTask(), Rabbit.DisposeAsync().AsTask(), Redis.DisposeAsync().AsTask());
    }
}

[CollectionDefinition("commerce")]
public class CommerceCollection : ICollectionFixture<CommerceStackFixture>;
