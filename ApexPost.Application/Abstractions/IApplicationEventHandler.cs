namespace ApexPost.Application.Abstractions;

public interface IApplicationEventHandler
{
    Type EventType { get; }
    Task HandleAsync(object applicationEvent, CancellationToken cancellationToken = default);
}

public interface IApplicationEventHandler<in TEvent> : IApplicationEventHandler
{
    Task HandleAsync(TEvent applicationEvent, CancellationToken cancellationToken = default);

    Type IApplicationEventHandler.EventType => typeof(TEvent);

    Task IApplicationEventHandler.HandleAsync(object applicationEvent, CancellationToken cancellationToken) =>
        HandleAsync((TEvent)applicationEvent, cancellationToken);
}
