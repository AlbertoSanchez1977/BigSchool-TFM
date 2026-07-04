using BigSchool.Application.Notifications.Interfaces.Repositories;
using BigSchool.Domain.Notifications.Entities;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using BigSchool.Infrastructure.SharedKernel.Persistence.Repositories;

namespace BigSchool.Infrastructure.Notifications.Persistence.Repositories;

public class ContactRepository : EFRepository<Contact, int>, IContactRepository
{
    public ContactRepository(BigSchoolDbContext context) : base(context) { }
}
