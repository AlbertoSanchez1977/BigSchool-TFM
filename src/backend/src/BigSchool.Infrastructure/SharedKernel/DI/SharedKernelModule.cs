using Autofac;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Domain.SharedKernel.Interfaces;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Module = Autofac.Module;

namespace BigSchool.Infrastructure.SharedKernel.DI;

public sealed class SharedKernelModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Application.SharedKernel.* (behaviors, bus abstractions consumers, exchange rate provider, outbox, etc.)
        builder.RegisterAssemblyTypes(typeof(ApiResponse).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Application.SharedKernel"))
            .AsImplementedInterfaces();

        // Infrastructure.SharedKernel.* (DbConnectionFactory, ExchangeRateApiClient, InMemoryIntegrationEventBus,
        // IntegrationEventOutbox, OutboxDispatcher, etc.)
        builder.RegisterAssemblyTypes(typeof(BigSchoolDbContext).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Infrastructure.SharedKernel"))
            .AsImplementedInterfaces();

        // DbContext como sí-mismo y como IUnitOfWork (scoped)
        builder.Register(ctx =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<BigSchoolDbContext>();
            var configuration = ctx.Resolve<Microsoft.Extensions.Configuration.IConfiguration>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            optionsBuilder.UseMySql(connectionString!, ServerVersion.AutoDetect(connectionString!));
            var mediator = ctx.Resolve<IMediator>();
            return new BigSchoolDbContext(optionsBuilder.Options, mediator);
        })
        .AsSelf()
        .As<IUnitOfWork>()
        .InstancePerLifetimeScope();
    }
}
