using BigSchool.Application.Notifications.Interfaces.Repositories;
using BigSchool.Domain.Notifications.Entities;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using BigSchool.Infrastructure.SharedKernel.Persistence.Repositories;

namespace BigSchool.Infrastructure.Notifications.Persistence.Repositories;

public class EmailLogRepository : EFRepository<EmailLog, int>, IEmailLogRepository
{
    public EmailLogRepository(BigSchoolDbContext context) : base(context) { }
}
