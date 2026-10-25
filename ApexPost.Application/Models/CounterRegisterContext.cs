using ApexPost.Domain.Entities;

namespace ApexPost.Application.Models;

public sealed record CounterRegisterContext(
    Branch Branch,
    IReadOnlyList<ProductInventorySummary> Products,
    IReadOnlyList<ShippingSchedule> ShippingSchedules,
    IReadOnlyList<CounterTerminal> Terminals,
    IReadOnlyList<CounterTransaction> RecentTransactions);
