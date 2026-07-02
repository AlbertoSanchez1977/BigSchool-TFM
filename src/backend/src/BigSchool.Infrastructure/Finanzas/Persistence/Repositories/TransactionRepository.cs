using BigSchool.Domain.Finanzas.Entities;
using BigSchool.Application.Finanzas.Interfaces.Repositories;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using BigSchool.Infrastructure.SharedKernel.Persistence.Repositories;

namespace BigSchool.Infrastructure.Finanzas.Persistence.Repositories;

public class TransactionRepository : EFRepository<Transaction, int>, ITransactionRepository
{
    public TransactionRepository(BigSchoolDbContext context) : base(context)
    {
    }
}
