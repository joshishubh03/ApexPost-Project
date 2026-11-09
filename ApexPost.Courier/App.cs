using Microsoft.Extensions.DependencyInjection;
using ApexPost.Courier.Core.Auth;
using ApexPost.Courier.Core.Sync;

namespace ApexPost.Courier;

public sealed class App : Application
{
    private readonly AppShell shell;

    public App(AppShell shell, SyncCoordinator sync, SessionEvents session)
    {
        this.shell = shell;

        // D10: a 401 clears the token (AuthHeaderHandler) and sends the courier back to the Login page.
        session.SignedOut += (_, reason) => MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Shell.Current.GoToAsync("//login");
            await Shell.Current.DisplayAlert("Signed out", reason, "OK");
        });

        // D9: upload Pending scans whenever connectivity returns (and once now if online).
        sync.Start();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(shell) { Title = "ApexPost Courier" };
}

public sealed class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        Items.Add(new TabBar
        {
            Items =
            {
                Tab("Login", "login", () => services.GetRequiredService<Pages.LoginPage>()),
                Tab("New Scan", "newscan", () => services.GetRequiredService<Pages.NewScanPage>()),
                Tab("Pending", "pending", () => services.GetRequiredService<Pages.PendingScansPage>()),
                Tab("Sync", "sync", () => services.GetRequiredService<Pages.SyncStatusPage>())
            }
        });
    }

    private static ShellContent Tab(string title, string route, Func<Page> create) => new()
    {
        Title = title,
        Route = route,
        ContentTemplate = new DataTemplate(() => create())
    };
}
