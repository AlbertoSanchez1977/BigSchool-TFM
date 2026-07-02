using System.Data;
using BigSchool.Application.SharedKernel.Configuration;
using Microsoft.Extensions.Options;
using MySqlConnector;
using BigSchool.Application.SharedKernel.Interfaces;

namespace BigSchool.Infrastructure.SharedKernel.Persistence;

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
