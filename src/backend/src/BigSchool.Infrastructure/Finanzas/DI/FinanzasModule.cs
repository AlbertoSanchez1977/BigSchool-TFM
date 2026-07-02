using Autofac;
using BigSchool.Application.Finanzas.Interfaces.Repositories;
using BigSchool.Infrastructure.Finanzas.Persistence.Repositories;
using Module = Autofac.Module;

namespace BigSchool.Infrastructure.Finanzas.DI;

public sealed class FinanzasModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterAssemblyTypes(typeof(ITransactionRepository).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Application.Finanzas"))
            .AsImplementedInterfaces();

        builder.RegisterAssemblyTypes(typeof(TransactionRepository).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Infrastructure.Finanzas"))
            .AsImplementedInterfaces();
    }
}
