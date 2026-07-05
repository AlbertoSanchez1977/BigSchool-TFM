using BigSchool.Domain.Finance.Entities;
using BigSchool.Application.SharedKernel.Interfaces;

namespace BigSchool.Application.Finance.Interfaces.Repositories;

public interface ITransactionRepository : IRepository<Transaction, int>
{
}
