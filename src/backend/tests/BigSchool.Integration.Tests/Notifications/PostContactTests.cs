using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Notifications;

[Collection(IntegrationCollection.Name)]
public class PostContactTests : NotificationEndpointTestBase
{
    public PostContactTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_ValidContact_PersistsContactAndSingleAckEmailLog()
    {
        var client = Factory.CreateClient(); // AllowAnonymous
        var resp = await client.PostAsJsonAsync("/api/v1/contacts",
            new { fullName = "Ada Lovelace", email = "ada@example.com", message = "Hola" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        // Atomicidad UoW componible: Contact + EmailLog persistidos en la misma transacción.
        (await CountAsync("SELECT COUNT(*) FROM Contacts WHERE Email='ada@example.com'")).Should().Be(1);
        // EXACTAMENTE 1: si SendContactAckEmailOnContactSubmittedHandler (INotificationHandler) se
        // registrara también en Autofac (además de en MediatR), Publish lo invocaría 2 veces vía
        // GetServices → 2 EmailLogs. Este assert caza esa regresión de doble instanciación.
        (await CountAsync("SELECT COUNT(*) FROM EmailLogs WHERE Recipient='ada@example.com' AND Type=2 AND IdUser IS NULL")).Should().Be(1);
        // Flujo A es DomainEvent intra-módulo (no Outbox): confirma que no se coló ninguna fila ahí.
        (await CountAsync("SELECT COUNT(*) FROM OutboxMessages")).Should().Be(0);
    }

    [Fact]
    public async Task Post_InvalidData_Returns400_AndPersistsNothing()
    {
        var resp = await Factory.CreateClient().PostAsJsonAsync("/api/v1/contacts",
            new { fullName = "", email = "no-email", message = "" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CountAsync("SELECT COUNT(*) FROM Contacts")).Should().Be(0);
        (await CountAsync("SELECT COUNT(*) FROM EmailLogs")).Should().Be(0);
    }
}
