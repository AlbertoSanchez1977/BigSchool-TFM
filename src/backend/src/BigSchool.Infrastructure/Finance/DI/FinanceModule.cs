using Autofac;
using BigSchool.Application.Finance.Interfaces.Repositories;
using BigSchool.Infrastructure.Finance.Persistence.Repositories;
using Module = Autofac.Module;

namespace BigSchool.Infrastructure.Finance.DI;

public sealed class FinanceModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterAssemblyTypes(typeof(ITransactionRepository).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Application.Finance"))
            .AsImplementedInterfaces();

        builder.RegisterAssemblyTypes(typeof(TransactionRepository).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Infrastructure.Finance"))
            .AsImplementedInterfaces();
    }
}
