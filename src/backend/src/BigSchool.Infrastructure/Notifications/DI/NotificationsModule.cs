using Autofac;
using BigSchool.Application.Notifications.Commands.CreateContact;
using BigSchool.Infrastructure.Notifications.Persistence.Repositories;
using MediatR;
using Module = Autofac.Module;

namespace BigSchool.Infrastructure.Notifications.DI;

public sealed class NotificationsModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Application.Notifications: validators, IRequestHandler, IIntegrationEventHandler…
        // EXCLUYE los INotificationHandler (domain events): los posee MediatR; registrarlos aquí
        // los dispararía 2 veces en Publish (GetServices) → doble command.
        builder.RegisterAssemblyTypes(typeof(CreateContactCommand).Assembly)
            .Where(t => t.Namespace is not null
                        && t.Namespace.StartsWith("BigSchool.Application.Notifications")
                        && !IsMediatrNotificationHandler(t))
            .AsImplementedInterfaces();

        // Infrastructure.Notifications: repositorios.
        builder.RegisterAssemblyTypes(typeof(ContactRepository).Assembly)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("BigSchool.Infrastructure.Notifications"))
            .AsImplementedInterfaces();
    }

    internal static bool IsMediatrNotificationHandler(Type t) =>
        t.GetInterfaces().Any(i => i.IsGenericType
            && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>));
}
