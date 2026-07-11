using BigSchool.Domain.Finance.Entities;
using BigSchool.Application.Finance.Interfaces.Repositories;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using BigSchool.Infrastructure.SharedKernel.Persistence.Repositories;

namespace BigSchool.Infrastructure.Finance.Persistence.Repositories;

public class TransactionRepository : EFRepository<Transaction, int>, ITransactionRepository
{
    public TransactionRepository(BigSchoolDbContext context) : base(context)
    {
    }
}
