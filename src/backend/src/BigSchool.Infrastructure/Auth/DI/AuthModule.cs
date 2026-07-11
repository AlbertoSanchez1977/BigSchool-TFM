using System.Linq;
using Autofac;
using BigSchool.Application.Auth.Interfaces.Repositories;
using BigSchool.Infrastructure.Auth.Persistence.Repositories;
using MediatR;
using Module = Autofac.Module;

namespace BigSchool.Infrastructure.Auth.DI;

public sealed class AuthModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // EXCLUYE los INotificationHandler (domain events): los posee MediatR; registrarlos aquí
        // los dispararía 2 veces en Publish (GetServices) → 2 filas de outbox → 2 welcomes.
        builder.RegisterAssemblyTypes(typeof(IUserRepository).Assembly)
            .Where(t => t.Namespace is not null
                        && t.Namespace.StartsWith("BigSchool.Application.Auth")
                        && !IsMediatrNotificationHandler(t))
            .AsImplementedInterfaces();

        builder.RegisterAssemblyTypes(typeof(UserRepository).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Infrastructure.Auth"))
            .AsImplementedInterfaces();
    }

    internal static bool IsMediatrNotificationHandler(Type t) =>
        t.GetInterfaces().Any(i => i.IsGenericType
            && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>));
}
