namespace ApexPost.Application.Abstractions;

public interface IApplicationEventPublisher
{
    Task PublishAsync<TEvent>(TEvent applicationEvent, CancellationToken cancellationToken = default);
}
