using BigSchool.Domain.Finanzas.Entities;
using BigSchool.Application.SharedKernel.Interfaces;

namespace BigSchool.Application.Finanzas.Interfaces.Repositories;

public interface ITransactionRepository : IRepository<Transaction, int>
{
}
