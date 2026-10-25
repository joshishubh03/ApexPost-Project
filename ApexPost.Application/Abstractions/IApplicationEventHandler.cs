namespace ApexPost.Application.Abstractions;

public interface IApplicationEventHandler
{
    IReadOnlyCollection<Type> EventTypes { get; }
    Task HandleAsync(object applicationEvent, CancellationToken cancellationToken = default);
}

public interface IApplicationEventHandler<in TEvent> : IApplicationEventHandler
{
    Task HandleAsync(TEvent applicationEvent, CancellationToken cancellationToken = default);

    IReadOnlyCollection<Type> IApplicationEventHandler.EventTypes => [typeof(TEvent)];

    Task IApplicationEventHandler.HandleAsync(object applicationEvent, CancellationToken cancellationToken) =>
        HandleAsync((TEvent)applicationEvent, cancellationToken);
}
