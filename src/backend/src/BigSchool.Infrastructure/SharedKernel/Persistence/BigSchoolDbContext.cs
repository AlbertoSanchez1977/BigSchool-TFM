using BigSchool.Application.SharedKernel.Events;
using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.Finanzas.Entities;
using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.Rag.Entities;
using BigSchool.Domain.SharedKernel.Entities;
using BigSchool.Domain.SharedKernel.Interfaces;
using BigSchool.Infrastructure.SharedKernel.IntegrationEvents;
using BigSchool.Infrastructure.SharedKernel.Persistence.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BigSchool.Infrastructure.SharedKernel.Persistence;

public class BigSchoolDbContext : DbContext, IUnitOfWork
{
    private readonly IMediator _mediator;

    public BigSchoolDbContext(DbContextOptions<BigSchoolDbContext> options, IMediator mediator)
        : base(options)
    {
        _mediator = mediator;
    }

    // Aggregate Roots — acceso principal
    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();

    // Entidades hijas — DbSet necesario para EF Core migrations/queries
    // El acceso de escritura se hace siempre a través del Aggregate Root
    public DbSet<SubCategory> SubCategories => Set<SubCategory>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Valuation> Valuations => Set<Valuation>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<Disposal> Disposals => Set<Disposal>();

    // public DbSet<RagDocument> RagDocuments => Set<RagDocument>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public async Task<int> SaveChangesAsync(bool dispatchEvents = true)
    {
        if (!dispatchEvents)
            return await base.SaveChangesAsync(CancellationToken.None);

        // Componible: si ya hay transacción en curso (SaveChanges anidado desde un handler/command),
        // la dueña es la más externa → un único COMMIT, sin BEGIN anidado (que MySQL rechaza).
        var ownsTransaction = Database.CurrentTransaction is null && Database.IsRelational();
        await using var tx = ownsTransaction ? await Database.BeginTransactionAsync() : null;

        var result = await base.SaveChangesAsync(CancellationToken.None); // 1) agregado (Id ya asignado)
        await DispatchDomainEvents();                                     // 2) handlers reaccionan (Send command / encolan outbox)

        if (ChangeTracker.HasChanges())
            await base.SaveChangesAsync(CancellationToken.None);          // 3) persiste lo que quedó sin guardar (p.ej. fila de outbox)

        if (tx is not null) await tx.CommitAsync();
        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BigSchoolDbContext).Assembly);
        modelBuilder.SeedSubCategories();
        modelBuilder.SeedExchangeRates();
        modelBuilder.SeedCompanies();
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
