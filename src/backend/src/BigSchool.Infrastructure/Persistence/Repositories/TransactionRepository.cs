using BigSchool.Application.Interfaces.Repositories;
using BigSchool.Domain.Finanzas.Entities;

namespace BigSchool.Infrastructure.Persistence.Repositories;

public class TransactionRepository : EFRepository<Transaction, int>, ITransactionRepository
{
    public TransactionRepository(BigSchoolDbContext context) : base(context)
    {
    }
}
