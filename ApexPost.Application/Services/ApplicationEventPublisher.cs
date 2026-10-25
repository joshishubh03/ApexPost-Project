using ApexPost.Application.Abstractions;

namespace ApexPost.Application.Services;

/// <summary>
/// Dispatches an application event to its registered handlers.
/// </summary>
public sealed class ApplicationEventPublisher(
    IEnumerable<IApplicationEventHandler> handlers) : IApplicationEventPublisher
{
    public async Task PublishAsync<TPublishedEvent>(
        TPublishedEvent applicationEvent,
        CancellationToken cancellationToken = default)
    {
        if (applicationEvent is null)
        {
            throw new ArgumentNullException(nameof(applicationEvent));
        }

        foreach (var handler in handlers.Where(handler => handler.EventTypes.Any(eventType => eventType.IsInstanceOfType(applicationEvent))))
        {
            await handler.HandleAsync(applicationEvent, cancellationToken);
        }
    }
}
