namespace BigSchool.Application.SharedKernel.IntegrationEvents.Contracts;

public sealed record UserRegisteredIntegrationEvent(
    Guid EventId, DateTime OccurredOn, int IdUser, string Email, string FullName) : IIntegrationEvent;
