using System.Net.Http.Headers;
using BigSchool.Application.Auth.Interfaces.Services;
using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using BigSchool.Integration.Tests.Fixtures;
using Dapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MySqlConnector;

namespace BigSchool.Integration.Tests.Notifications;

/// <summary>Helpers específicos de los endpoints de Notifications (Contacts/Emails).</summary>
public abstract class NotificationEndpointTestBase : IntegrationTestBase
{
    protected NotificationEndpointTestBase(MySqlDatabaseFixture fixture) : base(fixture) { }

    protected async Task<(int Id, string Email)> SeedUserAsync(Currency baseCurrency = Currency.EUR)
    {
        var email = $"notif-{Guid.NewGuid():N}@test.com";
        var options = new DbContextOptionsBuilder<BigSchoolDbContext>()
            .UseMySql(Fixture.ConnectionString, ServerVersion.AutoDetect(Fixture.ConnectionString))
            .Options;
        await using var ctx = new BigSchoolDbContext(options, Mock.Of<IMediator>());
        var user = User.Create(email, "hash", "salt", "Notif User", baseCurrency);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync(dispatchEvents: false);
        return (user.IdUser, email);
    }

    protected HttpClient AuthenticatedClient(int userId, string email)
    {
        var jwt = Factory.Services.GetRequiredService<IJwtService>().GenerateToken(userId, email);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt.AccessToken);
        return client;
    }

    protected async Task<int> CountAsync(string sql) => await ScalarAsync<int>(sql);

    protected async Task<T> ScalarAsync<T>(string sql)
    {
        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        return await conn.ExecuteScalarAsync<T>(sql);
    }

    // ContactListItemDto: FullName/Email/Message string, CreatedAt "yyyy-MM-ddTHH:mm:ss..." (System.Text.Json).
    protected record ContactListItemResponse(int IdContact, string FullName, string Email, string Message, string CreatedAt);

    // EmailLogListItemDto: Recipient/Subject string, SentAt "yyyy-MM-ddTHH:mm:ss..." (System.Text.Json).
    protected record EmailLogListItemResponse(int IdEmailLog, int? IdUser, string Recipient, string Subject, short Type, string SentAt);
}
