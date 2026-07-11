using Autofac;
using BigSchool.Application.Investments.Interfaces.Repositories;
using BigSchool.Infrastructure.Investments.Persistence.Repositories;
using Module = Autofac.Module;

namespace BigSchool.Infrastructure.Investments.DI;

public sealed class InvestmentsModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterAssemblyTypes(typeof(ICompanyRepository).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Application.Investments"))
            .AsImplementedInterfaces();

        builder.RegisterAssemblyTypes(typeof(CompanyRepository).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Infrastructure.Investments"))
            .AsImplementedInterfaces();
    }
}
