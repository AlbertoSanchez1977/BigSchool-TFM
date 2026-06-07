using System.Data;

namespace BigSchool.Application.Interfaces;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
