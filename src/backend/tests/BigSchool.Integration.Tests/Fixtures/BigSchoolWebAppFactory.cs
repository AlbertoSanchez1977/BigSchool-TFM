using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BigSchool.Integration.Tests.Fixtures;

/// <summary>
/// Arranca la API real (pipeline completo: auth JwtBearer, MediatR, EF, Dapper) contra la BD de test.
/// Sobrescribe ConnectionStrings:DefaultConnection (clave única de conexión: la leen EF y Dapper).
/// El resto de config (Jwt:Secret/Issuer/Audience) sale del appsettings*.json del WebApi,
/// garantizando que el token acuñado por IJwtService y la validación JwtBearer comparten el mismo secreto.
/// </summary>
public sealed class BigSchoolWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public BigSchoolWebAppFactory(string connectionString) => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString
            });
        });
    }
}
