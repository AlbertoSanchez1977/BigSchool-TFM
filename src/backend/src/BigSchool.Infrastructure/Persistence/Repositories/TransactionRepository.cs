using BigSchool.Domain.Finanzas.Entities;
using BigSchool.Application.Finanzas.Interfaces.Repositories;

namespace BigSchool.Infrastructure.Persistence.Repositories;

public class TransactionRepository : EFRepository<Transaction, int>, ITransactionRepository
{
    public TransactionRepository(BigSchoolDbContext context) : base(context)
    {
    }
}
