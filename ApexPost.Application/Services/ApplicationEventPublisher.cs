using ApexPost.Application.Abstractions;

namespace ApexPost.Application.Services;

/// <summary>
/// Dispatches an application event to its registered handlers.
/// </summary>
public sealed class ApplicationEventPublisher<TEvent>(
    IEnumerable<IApplicationEventHandler<TEvent>> handlers) : IApplicationEventPublisher
{
    public async Task PublishAsync<TPublishedEvent>(
        TPublishedEvent applicationEvent,
        CancellationToken cancellationToken = default)
    {
        if (applicationEvent is not TEvent typedEvent)
        {
            return;
        }

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(typedEvent, cancellationToken);
        }
    }
}
