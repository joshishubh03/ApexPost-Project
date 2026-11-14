# Copilot log: AI-drafted tests with human review

GitHub Copilot drafted two pieces of test code. A person reviewed every line before anything was committed.
This log records each prompt, what Copilot produced, what we kept, changed or rejected and why, and the proof
that the kept tests can fail.

Copilot-assisted test files carry this comment on the first line:

```csharp
// Copilot-assisted: drafted with GitHub Copilot, reviewed and corrected by hand. See COPILOT-LOG.md.
```

| # | Target | Copilot output | Final file |
|---|---|---|---|
| 1 | Unit tests for `CourierShipmentService.ReplaceAsync` and `ListAsync` (Moq) | Section 1.2 | `ApexPost.UnitTests/Courier/CourierShipmentServiceCopilotTests.cs` |
| 2 | Gherkin feature and step definitions for shipment status updates (API level) | Section 2.2 | `ApexPost.Specs/Features/ShipmentStatusUpdates.feature`, `ApexPost.Specs/Steps/ShipmentStatusSteps.cs` |

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

> Paste Copilot's complete answer here, unedited (code block), before making any change.

```csharp
// (Copilot output)
```

### 1.3 Review decisions

| Generated test | Decision | Reason |
|---|---|---|
| *(test name)* | Kept / Changed / Rejected | *(for example: "Rejected: asserts only `Assert.NotNull(result)`; the mock returns the details whatever the service does, so the test cannot fail.")* |
| | | |
| | | |

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

## 2. A Gherkin feature with its step definitions

### 2.1 Prompt

Copilot Chat, with these files attached: `#ShipmentCreation.feature`, `#ShipmentCreationSteps.cs`,
`#ShipmentApiContext.cs`, `#Hooks.cs`, `#ShipmentApiHost.cs`, `#ShipmentsController.cs` (V2).

```text
Write a Reqnroll feature file ShipmentStatusUpdates.feature, in business language, for a courier updating a
shipment's status through the API (PATCH /api/v2/shipments/{trackingNumber}/status with body { "status": "..." }),
and its step definitions in ShipmentStatusSteps.cs.
Rules: Created -> PickedUp -> InTransit -> OutForDelivery -> Delivered or Failed. Any other change is refused with
409 Conflict. Only the Courier role may change a status: a token with only the Admin role gets 403 Forbidden.
An unknown tracking number gives 404.
Use a Background and a Scenario Outline with an Examples table for allowed and refused changes.
Tag the feature @api. Reuse ShipmentApiContext through constructor injection (no static fields) and
TestRunHooks.ApiHost.CreateClientWithRoles to get clients. Create shipments through POST /api/v2/shipments with
an Admin token and add their tracking numbers to context.CreatedTrackingNumbers so the existing AfterScenario
hook deletes them. Use regular-expression step definitions like ShipmentCreationSteps.
Use tracking numbers APX-51000001 and higher.
```

### 2.2 What Copilot produced

> Paste the generated feature file and step definitions here, unedited.

```gherkin
# (Copilot output: feature)
```

```csharp
// (Copilot output: steps)
```

### 2.3 Review decisions

| Generated item | Decision | Reason |
|---|---|---|
| *(scenario / step)* | Kept / Changed / Rejected | |
| | | |

Things to look for in this draft:

- Technical wording in the scenarios ("PATCH returns 409") instead of business language ("the change is refused").
- A client with both roles used for the 403 scenario: then the test passes or fails for the wrong reason.
- Step text that duplicates an existing step in `ShipmentCreationSteps` (Reqnroll reports an ambiguous step).
- Static fields used to pass the response between steps.
- Tracking numbers that clash with `ShipmentCreation.feature` (50000001-50000021).
- A Scenario Outline whose expected result is computed by calling the production status table (copying the
  implementation).

### 2.4 Final version

`ApexPost.Specs/Features/ShipmentStatusUpdates.feature` and `ApexPost.Specs/Steps/ShipmentStatusSteps.cs`
(steps class marked with the Copilot comment).

---

## 3. Proof that the generated tests are meaningful

Each kept test must **fail** when the behaviour it checks is broken. `scripts/prove-tests-fail.ps1` runs the tests
on the real code, applies one deliberate bug, runs them again (they must fail), restores the file byte for byte
and runs them a third time. The full logs are in `docs/test-results/`.

### 3.1 Break the page-size limit (tests from section 1)

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
| Real code | *(paste the summary line, e.g. "Passed! - Failed: 0, Passed: 10")* |
| Page size 51 allowed (bug) | *(e.g. "Failed! - Failed: 1 ... ListAsync_PageSizeAboveFifty_Throws")* |
| Restored | *(Passed)* |

### 3.2 Allow a forbidden status change (feature from section 2)

```powershell
powershell -ExecutionPolicy Bypass -File scripts/prove-tests-fail.ps1 `
    -Name "mutation-status-machine" `
    -File "ApexPost.Domain/Courier/CourierShipment.cs" `
    -Find "[CourierShipmentStatus.Created] = [CourierShipmentStatus.PickedUp]," `
    -Replace "[CourierShipmentStatus.Created] = [CourierShipmentStatus.PickedUp, CourierShipmentStatus.Delivered]," `
    -Project "ApexPost.Specs" `
    -Filter "FullyQualifiedName~ShipmentStatusUpdates"
```

| Run | Result |
|---|---|
| Real code | |
| Created -> Delivered allowed (bug) | |
| Restored | |

### 3.3 Generated test that was rejected (for the viva)

> Describe one generated test you rejected, quote it, and explain in two or three sentences why it was wrong
> and what replaced it.
