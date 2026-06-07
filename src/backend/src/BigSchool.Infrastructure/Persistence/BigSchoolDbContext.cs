using BigSchool.Application.Events;
using BigSchool.Domain.Entities;
using BigSchool.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BigSchool.Infrastructure.Persistence;

public class BigSchoolDbContext : DbContext, IUnitOfWork
{
    private readonly IMediator _mediator;

    public BigSchoolDbContext(DbContextOptions<BigSchoolDbContext> options, IMediator mediator)
        : base(options)
    {
        _mediator = mediator;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<RagDocument> RagDocuments => Set<RagDocument>();

    public async Task<int> SaveChangesAsync(bool dispatchEvents = true, CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(cancellationToken);

        if (dispatchEvents)
        {
            await DispatchDomainEvents();
        }

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BigSchoolDbContext).Assembly);
    }

    private async Task DispatchDomainEvents()
    {
        var entities = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entities.SelectMany(e => e.DomainEvents).ToList();
        entities.ForEach(e => e.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
        {
            var notificationType = typeof(DomainEventNotification<>)
                .MakeGenericType(domainEvent.GetType());
            var notification = Activator.CreateInstance(notificationType, domainEvent);
            await _mediator.Publish(notification!);
        }
    }
}
