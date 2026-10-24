namespace ApexPost.Application.Abstractions;

public interface IApplicationEventHandler<in TEvent>
{
    Task HandleAsync(TEvent applicationEvent, CancellationToken cancellationToken = default);
}
