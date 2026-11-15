namespace ApexPost.Specs.Support;

/// <summary>
/// Test-run hooks: one in-memory API host and one disposable SQL Server database for the whole run, the BDD
/// equivalent of an xUnit collection fixture. This is a shared, read-mostly resource; scenario state never lives
/// here (it is in the context classes, injected per scenario).
/// </summary>
[Binding]
public sealed class TestRunHooks
{
    private static ShipmentApiHost? apiHost;

    public static ShipmentApiHost ApiHost =>
        apiHost ?? throw new InvalidOperationException("The API host was not started (BeforeTestRun did not run).");

    [BeforeTestRun]
    public static async Task StartApiAsync()
    {
        apiHost = new ShipmentApiHost();
        await apiHost.MigrateAsync();
    }

    [AfterTestRun]
    public static async Task StopApiAsync()
    {
        if (apiHost is null)
        {
            return;
        }

        await apiHost.DropDatabaseAsync();
        await apiHost.DisposeAsync();
        apiHost = null;
    }
}

/// <summary>Set-up and clean-up for the API scenarios (@api).</summary>
[Binding]
public sealed class ApiHooks(ShipmentApiContext context)
{
    [AfterScenario("api")]
    public async Task DeleteShipmentsCreatedByTheScenarioAsync()
    {
        await TestRunHooks.ApiHost.DeleteShipmentsAsync(context.CreatedTrackingNumbers);
        context.Dispose();
    }
}

/// <summary>Set-up and clean-up for the phone sync scenarios (@device).</summary>
[Binding]
public sealed class DeviceHooks(CourierSyncContext world)
{
    [BeforeScenario("device")]
    public void OpenPhoneStoreAndServer() => world.Open();

    [AfterScenario("device")]
    public void ClosePhoneStoreAndDeleteItsFiles() => world.Close();
}
