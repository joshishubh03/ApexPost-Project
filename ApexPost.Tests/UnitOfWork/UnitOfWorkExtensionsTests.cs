using ApexPost.Application.Abstractions;

namespace ApexPost.Tests.UnitOfWork;

public sealed class UnitOfWorkExtensionsTests
{
    [Fact]
    public async Task Use_case_with_a_result_runs_in_one_transaction_and_commits_once()
    {
        var unitOfWork = new SpyUnitOfWork();

        var result = await unitOfWork.ExecuteUseCaseAsync(() => Task.FromResult(42));

        Assert.Equal(42, result);
        Assert.Equal(1, unitOfWork.Transactions);
        Assert.Equal(1, unitOfWork.Commits);
    }

    [Fact]
    public async Task Use_case_without_a_result_runs_in_one_transaction_and_commits_once()
    {
        var unitOfWork = new SpyUnitOfWork();
        var ran = false;

        await unitOfWork.ExecuteUseCaseAsync(() =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        Assert.True(ran);
        Assert.Equal(1, unitOfWork.Transactions);
        Assert.Equal(1, unitOfWork.Commits);
    }

    [Fact]
    public async Task Nested_use_cases_join_the_outer_transaction_and_commit_once()
    {
        var unitOfWork = new SpyUnitOfWork();

        await unitOfWork.ExecuteUseCaseAsync(async () =>
        {
            await unitOfWork.ExecuteUseCaseAsync(() => Task.CompletedTask);
            await unitOfWork.ExecuteUseCaseAsync(() => Task.FromResult("inner"));
        });

        Assert.Equal(1, unitOfWork.Transactions);
        Assert.Equal(1, unitOfWork.Commits);
    }

    [Fact]
    public async Task Failing_use_case_never_commits()
    {
        var unitOfWork = new SpyUnitOfWork();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.ExecuteUseCaseAsync(() => throw new InvalidOperationException("rule broken")));

        Assert.Equal(0, unitOfWork.Commits);
    }

    [Fact]
    public async Task Null_arguments_are_rejected()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            UnitOfWorkExtensions.ExecuteUseCaseAsync(null!, () => Task.CompletedTask));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new SpyUnitOfWork().ExecuteUseCaseAsync((Func<Task>)null!));
    }
}
