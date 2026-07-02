using System.Data;

namespace BigSchool.Application.SharedKernel.Interfaces;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
