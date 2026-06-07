using System.Data;
using BigSchool.Application.Configuration;
using BigSchool.Application.Interfaces;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace BigSchool.Infrastructure.Persistence;

public class DbConnectionMySqlFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionMySqlFactory(IOptions<AppSettings> settings)
    {
        _connectionString = settings?.Value?.ConnectionString
            ?? throw new ArgumentNullException(nameof(settings), "ConnectionString is required in AppSettings.");
    }

    public IDbConnection CreateConnection()
    {
        return new MySqlConnection(_connectionString);
    }
}
