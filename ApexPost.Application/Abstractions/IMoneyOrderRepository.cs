using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions;

public interface IMoneyOrderRepository
{
    Task<IReadOnlyList<MoneyOrder>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<MoneyOrder> moneyOrders, CancellationToken cancellationToken = default);
}
