using Autofac;
using BigSchool.Application.Auth.Interfaces.Repositories;
using BigSchool.Infrastructure.Auth.Persistence.Repositories;
using Module = Autofac.Module;

namespace BigSchool.Infrastructure.Auth.DI;

public sealed class AuthModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterAssemblyTypes(typeof(IUserRepository).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Application.Auth"))
            .AsImplementedInterfaces();

        builder.RegisterAssemblyTypes(typeof(UserRepository).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Infrastructure.Auth"))
            .AsImplementedInterfaces();
    }
}
