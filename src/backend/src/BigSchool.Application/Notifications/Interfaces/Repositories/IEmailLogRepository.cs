using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Domain.Notifications.Entities;

namespace BigSchool.Application.Notifications.Interfaces.Repositories;

public interface IEmailLogRepository : IRepository<EmailLog, int> { }
