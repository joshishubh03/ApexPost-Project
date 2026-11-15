# Copilot log: AI-drafted tests with human review

GitHub Copilot drafted a set of unit tests for the shipment service. A person reviewed every line before anything was committed.
This log records the prompt, what Copilot produced, what we kept, changed or rejected and why, and the proof
that the kept tests can fail.

Copilot-assisted test files carry this comment on the first line:

```csharp
// Copilot-assisted: drafted with GitHub Copilot, reviewed and corrected by hand. See COPILOT-LOG.md.
```

| # | Target | Copilot output | Final file |
|---|---|---|---|
| 1 | Unit tests for `CourierShipmentService.ReplaceAsync` and `ListAsync` (Moq) | Section 1.2 | `ApexPost.UnitTests/Courier/CourierShipmentServiceCopilotTests.cs` |

## How each draft was reviewed

Every generated test was checked against this list before it was kept:

| Check | What we look for | Why it matters |
|---|---|---|
| Assertion strength | `Assert.NotNull(result)` or "does not throw" with nothing else | A test that cannot fail proves nothing |
| Expected values | Expected values copied from the code under test, or simply wrong (for example page size 50 treated as invalid) | A test that encodes the bug turns it into a "requirement" |
| Mock set-up | `Setup` that never matches (wrong argument), `It.IsAny` everywhere, missing `Verify`, a mock that returns `null` and hides the path under test | The test passes for the wrong reason |
| Edge cases | Boundaries (1, 50, 51 for page size; 0 and 70 kg), unknown tracking numbers, final statuses, lower-case input | Bugs live at the edges |
| Copying the implementation | The test recomputes the result the same way the code does (for example re-running `IsAllowedTransition` to get the expected value) | It will agree with a wrong implementation |
| Determinism | `DateTime.Now`, `Guid` ordering, shared static state, test order | Flaky tests destroy trust in the suite |
| Style | AAA, `Method_Scenario_ExpectedResult`, business language in Gherkin, no static step state | Consistency with `TESTING-STRATEGY.md` |

---

## 1. Unit tests for a service (Moq)

### 1.1 Prompt

Copilot Chat in Visual Studio, with these files attached: `#CourierShipmentService.cs`,
`#ICourierShipmentRepository.cs`, `#CourierShipment.cs`, `#CourierShipmentModels.cs`,
`#CourierShipmentServiceMockTests.cs`.

```text
Write xUnit unit tests with Moq for CourierShipmentService.ReplaceAsync and CourierShipmentService.ListAsync.
Follow the style of CourierShipmentServiceMockTests: test names Method_Scenario_ExpectedResult, Arrange/Act/Assert
comments, a Mock<ICourierShipmentRepository>, a Mock<IUnitOfWork> whose ExecuteInTransactionAsync runs the
operation, and FixedClock for time (no DateTime.Now).
Cover: a normal replace (weight and recipient change, UpdatedAtUtc comes from the clock), replacing a Delivered
shipment, an unknown tracking number, a lower-case tracking number, page size 1, 50, 51 and 0, page 0,
a city filter with surrounding spaces, and a null query.
Use ReturnsAsync, It.Is with real conditions, and Verify with Times.
Name the class CourierShipmentServiceCopilotTests.
```

### 1.2 What Copilot produced

Copilot's answer, unedited (11 tests). The first-line marker was requested in the prompt.

```csharp
// Copilot-assisted: drafted with GitHub Copilot, reviewed and corrected by hand. See COPILOT-LOG.md.
using ApexPost.Application.Abstractions;
using ApexPost.Application.Courier;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Courier;
using ApexPost.UnitTests.TestDoubles;
using Moq;

namespace ApexPost.UnitTests.Courier;

public sealed class CourierShipmentServiceCopilotTests
{
    private const string TrackingNumber = "APX-10000001";
    private static readonly DateTimeOffset Now = FixedClock.DefaultNow;

    private readonly Mock<ICourierShipmentRepository> repository = new();
    private readonly Mock<IUnitOfWork> unitOfWork = new();
    private readonly CourierShipmentService service;

    public CourierShipmentServiceCopilotTests()
    {
        unitOfWork
            .Setup(work => work.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<bool>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<bool>> operation, CancellationToken token) => operation(token));

        service = new CourierShipmentService(repository.Object, unitOfWork.Object, new FixedClock(Now));
    }

    [Fact]
    public async Task ReplaceAsync_ValidChange_UpdatesWeightRecipientAndUpdatedAt()
    {
        // Arrange
        var command = new ReplaceCourierShipmentCommand(
            3.5m,
            new Recipient("Jordan Example", "+15550100123", "12 Elm Row", "Maplewood"));

        var existing = new CourierShipment(
            Guid.NewGuid(),
            TrackingNumber,
            2.5m,
            new Recipient("Jordan Example", "+15550100123", "12 Elm Row", "Maplewood"),
            CourierShipmentStatus.Created,
            Now.AddDays(-1),
            Now.AddDays(-1));

        repository
            .Setup(repo => repo.FindByTrackingNumberAsync(TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        repository
            .Setup(repo => repo.GetDetailsAsync(TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CourierShipmentDetails(
                TrackingNumber,
                CourierShipmentStatus.Created,
                3.5m,
                command.Recipient,
                Now.AddDays(-1),
                Now,
                null));

        // Act
        var result = await service.ReplaceAsync(TrackingNumber, command);

        // Assert
        Assert.Equal(3.5m, result.WeightKilograms);
        Assert.Equal("Jordan Example", result.Recipient.Name);
        Assert.Equal("Maplewood", result.Recipient.City);

        repository.Verify(repo => repo.Update(It.Is<CourierShipment>(shipment =>
            shipment.TrackingNumber == TrackingNumber &&
            shipment.WeightKilograms == 3.5m &&
            shipment.Recipient.City == "Maplewood" &&
            shipment.UpdatedAtUtc == Now)), Times.Once);
    }

    [Fact]
    public async Task ReplaceAsync_DeliveredShipment_ThrowsAndDoesNotUpdate()
    {
        // Arrange
        var command = new ReplaceCourierShipmentCommand(
            3.5m,
            new Recipient("Jordan Example", "+15550100123", "12 Elm Row", "Maplewood"));

        var existing = new CourierShipment(
            Guid.NewGuid(),
            TrackingNumber,
            2.5m,
            new Recipient("Jordan Example", "+15550100123", "12 Elm Row", "Maplewood"),
            CourierShipmentStatus.Delivered,
            Now.AddDays(-10),
            Now.AddDays(-10));

        repository
            .Setup(repo => repo.FindByTrackingNumberAsync(TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        var exception = await Assert.ThrowsAsync<ShipmentRuleViolationException>(() =>
            service.ReplaceAsync(TrackingNumber, command));

        // Assert
        Assert.Contains("can no longer be edited", exception.Message);
        repository.Verify(repo => repo.Update(It.IsAny<CourierShipment>()), Times.Never);
    }

    [Fact]
    public async Task ReplaceAsync_UnknownTrackingNumber_ThrowsShipmentNotFound()
    {
        // Arrange
        repository
            .Setup(repo => repo.FindByTrackingNumberAsync(TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CourierShipment?)null);

        // Act
        await Assert.ThrowsAsync<ShipmentNotFoundException>(() =>
            service.ReplaceAsync(
                TrackingNumber,
                new ReplaceCourierShipmentCommand(
                    2.0m,
                    new Recipient("Jordan Example", "+15550100123", "12 Elm Row", "Maplewood"))));

        // Assert
        repository.Verify(repo => repo.Update(It.IsAny<CourierShipment>()), Times.Never);
    }

    [Fact]
    public async Task ReplaceAsync_LowerCaseTrackingNumber_IsNormalizedBeforeLookup()
    {
        // Arrange
        var command = new ReplaceCourierShipmentCommand(
            3.5m,
            new Recipient("Jordan Example", "+15550100123", "12 Elm Row", "Maplewood"));

        var existing = new CourierShipment(
            Guid.NewGuid(),
            TrackingNumber,
            2.5m,
            new Recipient("Jordan Example", "+15550100123", "12 Elm Row", "Maplewood"),
            CourierShipmentStatus.Created,
            Now.AddDays(-1),
            Now.AddDays(-1));

        repository
            .Setup(repo => repo.FindByTrackingNumberAsync(TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        repository
            .Setup(repo => repo.GetDetailsAsync(TrackingNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CourierShipmentDetails(
                TrackingNumber,
                CourierShipmentStatus.Created,
                3.5m,
                command.Recipient,
                Now.AddDays(-1),
                Now,
                null));

        // Act
        await service.ReplaceAsync("  apx-10000001  ", command);

        // Assert
        repository.Verify(repo => repo.FindByTrackingNumberAsync(
            TrackingNumber,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListAsync_PageSizeOne_UsesValidQuery()
    {
        // Arrange
        var query = new CourierShipmentQuery(1, 1, null, null);

        repository
            .Setup(repo => repo.ListAsync(
                It.Is<CourierShipmentQuery>(q => q.Page == 1 && q.PageSize == 1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CourierShipmentDetails>([], 1, 1, 0));

        // Act
        var result = await service.ListAsync(query);

        // Assert
        Assert.Equal(1, result.PageSize);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ListAsync_PageSizeFifty_IsAllowed()
    {
        // Arrange
        var query = new CourierShipmentQuery(1, 50, null, null);

        repository
            .Setup(repo => repo.ListAsync(
                It.Is<CourierShipmentQuery>(q => q.PageSize == 50),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CourierShipmentDetails>([], 1, 50, 0));

        // Act
        var result = await service.ListAsync(query);

        // Assert
        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task ListAsync_PageSizeFiftyOne_Throws()
    {
        // Arrange
        var query = new CourierShipmentQuery(1, 51, null, null);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ListAsync(query));

        // Assert
        Assert.Equal("pageSize", exception.ParamName);
    }

    [Fact]
    public async Task ListAsync_PageSizeZero_Throws()
    {
        // Arrange
        var query = new CourierShipmentQuery(1, 0, null, null);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ListAsync(query));

        // Assert
        Assert.Equal("pageSize", exception.ParamName);
    }

    [Fact]
    public async Task ListAsync_PageZero_Throws()
    {
        // Arrange
        var query = new CourierShipmentQuery(0, 20, null, null);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ListAsync(query));

        // Assert
        Assert.Equal("page", exception.ParamName);
    }

    [Fact]
    public async Task ListAsync_CityFilterTrimsWhitespaceBeforeRepositoryCall()
    {
        // Arrange
        var query = new CourierShipmentQuery(1, 20, null, "  Maplewood  ");

        repository
            .Setup(repo => repo.ListAsync(
                It.Is<CourierShipmentQuery>(q => q.City == "Maplewood"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CourierShipmentDetails>([], 1, 20, 0));

        // Act
        await service.ListAsync(query);

        // Assert
        repository.Verify(repo => repo.ListAsync(
            It.Is<CourierShipmentQuery>(q => q.City == "Maplewood"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListAsync_NullQuery_ThrowsArgumentNullException()
    {
        // Act
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ListAsync(null!));
    }
}
```

**Checked against the real code before any change:** the service passes `nameof(query.PageSize)` and
`nameof(query.Page)` to `ArgumentOutOfRangeException.ThrowIfLessThan` / `ThrowIfGreaterThan`, so the exception's
`ParamName` is `"PageSize"` or `"Page"`. Copilot's three boundary tests expect `"pageSize"` and `"page"`, so they
fail on correct code. That was the first thing fixed (section 1.3).

### 1.3 Review decisions

| Generated test | Decision | Reason |
|---|---|---|
| `ReplaceAsync_ValidChange_UpdatesWeightRecipientAndUpdatedAt` | **Changed** | The `Verify` on `Update` (weight, `UpdatedAtUtc == Now`, `Times.Once`) was good and is kept. Two problems around it: (1) the "new" recipient is identical to the existing one (Jordan Example, Maplewood), so the test could not notice if the recipient were never replaced; (2) the three `Assert.Equal` lines check `result`, which is exactly what the mocked `GetDetailsAsync` was told to return, so they pass whatever `ReplaceAsync` does (asserting the mock). Now a different recipient (Sam Example, Riverton) is used and checked in the `Verify`, the `result` asserts are removed, and the unit of work is verified `Times.Once`. |
| `ReplaceAsync_DeliveredShipment_ThrowsAndDoesNotUpdate` | Kept | Right exception, `Update` verified `Times.Never`. Only added `StringComparison.Ordinal` to the message check. |
| `ReplaceAsync_UnknownTrackingNumber_ThrowsShipmentNotFound` | Kept | Correct path and `Times.Never` on `Update`. |
| `ReplaceAsync_LowerCaseTrackingNumber_IsNormalizedBeforeLookup` | Kept | Verifies the lookup with the normalised number `Times.Once`. Added the same check for the final read (`GetDetailsAsync`). |
| `ListAsync_PageSizeOne_UsesValidQuery` | **Rejected** | Its only asserts are `result.PageSize == 1` and `result.Items` empty, and `result` is the `PagedResult` the mock was set up to return, so the test checks its own setup. If the service sent a different query, the `Setup` would not match, Moq would return `null` and the test would crash with a `NullReferenceException` instead of a clear assertion; nothing verifies what the repository actually received. Replaced by a hand-written `[Theory]`, `ListAsync_PageSizeInsideTheLimits_PassesTheQueryToTheRepository` (rows 1 and 50), which uses `Verify` on the query passed to the repository. |
| `ListAsync_PageSizeFifty_IsAllowed` | **Changed** (merged) | Same "asserting the mock" problem as above. Folded into the new `[Theory]` as the upper boundary row (50). |
| `ListAsync_PageSizeFiftyOne_Throws` | **Changed** | **Wrong expected value**: it expects `ParamName == "pageSize"`, but the service uses `nameof(query.PageSize)`, which is `"PageSize"`. The draft fails on correct code (see the run above). Fixed, and it now also verifies the repository is never queried. Merged into `ListAsync_PageOrPageSizeOutOfRange_ThrowsAndNeverQueries`. |
| `ListAsync_PageSizeZero_Throws` | **Changed** | Same wrong `ParamName` (`"pageSize"` instead of `"PageSize"`). Merged into the same `[Theory]`. |
| `ListAsync_PageZero_Throws` | **Changed** | Same mistake: expects `"page"`, the service reports `"Page"`. Merged into the same `[Theory]`. |
| `ListAsync_CityFilterTrimsWhitespaceBeforeRepositoryCall` | Kept | Good: `It.Is` with a real condition (`City == "Maplewood"`) and `Verify ... Times.Once`. The duplicated `Setup` condition was simplified to `It.IsAny` so the `Verify` alone decides the outcome. |
| `ListAsync_NullQuery_ThrowsArgumentNullException` | **Changed** | Had no Assert section. Now also checks that the repository was never called (`VerifyNoOtherCalls`). |

Summary: 4 kept, 6 changed, 1 rejected. The main lessons: Copilot **guessed parameter names** instead of reading
`nameof(query.PageSize)` (three tests failed on correct code), and it **asserted values that came from its own
mocks**, which makes a test pass whatever the service does.

Things to look for in this draft:

- `ListAsync` page size: **50 is valid, 51 is not**. Check that Copilot did not use `>=`.
- `ReplaceAsync` on a Delivered shipment throws `ShipmentRuleViolationException`, and `Update` must be verified
  `Times.Never`.
- The city filter: the service trims the city before calling the repository. The test needs
  `It.Is<CourierShipmentQuery>(q => q.City == "Maplewood")`, not `It.IsAny`.
- Does any test expect `ArgumentNullException` for a null query? (`ListAsync` throws it; `ReplaceAsync` with a null
  command does too.)

### 1.4 Final version

`ApexPost.UnitTests/Courier/CourierShipmentServiceCopilotTests.cs` (marked with the Copilot comment).

---

## 2. Proof that the generated tests are meaningful

Each kept test must be able to **fail** when the behaviour it checks is broken. `scripts/prove-tests-fail.ps1` runs the tests
on the real code, applies one deliberate bug, runs them again (they must fail), restores the file byte for byte
and runs them a third time. The full logs are in `docs/test-results/`.

### 2.1 Break the page-size limit

```powershell
powershell -ExecutionPolicy Bypass -File scripts/prove-tests-fail.ps1 `
    -Name "mutation-page-size" `
    -File "ApexPost.Application/Courier/CourierShipmentService.cs" `
    -Find "ThrowIfGreaterThan(query.PageSize, CourierShipmentQuery.MaximumPageSize" `
    -Replace "ThrowIfGreaterThan(query.PageSize, CourierShipmentQuery.MaximumPageSize + 1" `
    -Project "ApexPost.UnitTests" `
    -Filter "FullyQualifiedName~CourierShipmentServiceCopilotTests"
```

| Run | Result |
|---|---|
| Real code | `Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 254 ms - ApexPost.UnitTests.dll (net10.0)` |
| Page size 51 allowed (bug) | `pageSize: 51, parameter: "PageSize") [FAIL]`<br>`+ CategoryInfo          : NotSpecified: ([xUnit.net 00:0...geSize") [FAIL]:String) [], RemoteException`<br>`Failed!  - Failed:     1, Passed:    10, Skipped:     0, Total:    11, Duration: 242 ms - ApexPost.UnitTests.dll (net10.0)` |
| Restored | `Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 254 ms - ApexPost.UnitTests.dll (net10.0)` |

Full output: `docs/test-results/mutation-page-size.txt`.

### 2.2 Generated test that was rejected (for the viva)

Copilot wrote:

```csharp
[Fact]
public async Task ListAsync_PageSizeOne_UsesValidQuery()
{
    var query = new CourierShipmentQuery(1, 1, null, null);
    repository
        .Setup(repo => repo.ListAsync(
            It.Is<CourierShipmentQuery>(q => q.Page == 1 && q.PageSize == 1),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(new PagedResult<CourierShipmentDetails>([], 1, 1, 0));

    var result = await service.ListAsync(query);

    Assert.Equal(1, result.PageSize);
    Assert.Empty(result.Items);
}
```

It looks thorough, but both asserts read back the `PagedResult` the mock was told to return, so the test checks its
own set-up rather than the service. If the service sent the wrong query, the `Setup` would not match, Moq would
return `null`, and the test would crash with a `NullReferenceException` instead of explaining what went wrong.
It was replaced by `ListAsync_PageSizeInsideTheLimits_PassesTheQueryToTheRepository` (rows 1 and 50), which
verifies the query the repository actually received with `repository.Verify(..., Times.Once)`.
