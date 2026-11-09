using ApexPost.Courier.Core.Api;
using ApexPost.Courier.Core.Auth;

namespace ApexPost.Courier.Pages;

/// <summary>
/// Login (D11): obtain a development token from the API's /dev/token endpoint or paste one
/// (dotnet user-jwts / identity provider). The token is stored in SecureStorage only.
/// </summary>
public sealed class LoginPage : ContentPage
{
    private readonly ITokenStore tokens;
    private readonly Func<ApexApiClient> api;
    private readonly Entry courierId = new() { Placeholder = "Courier ID", Text = "courier-007" };
    private readonly Editor pastedToken = new() { Placeholder = "...or paste an access token", HeightRequest = 120, AutoSize = EditorAutoSizeOption.Disabled };
    private readonly Label status = new();

    public LoginPage(ITokenStore tokens, Func<ApexApiClient> api)
    {
        this.tokens = tokens;
        this.api = api;
        Title = "Login";

        var devToken = new Button { Text = "Get development token" };
        devToken.Clicked += async (_, _) => await GetDevelopmentTokenAsync();
        var usePasted = new Button { Text = "Use pasted token" };
        usePasted.Clicked += async (_, _) => await SaveAsync(pastedToken.Text);
        var signOut = new Button { Text = "Sign out", BackgroundColor = Colors.Gray };
        signOut.Clicked += async (_, _) =>
        {
            await tokens.ClearAsync();
            await RefreshAsync();
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children =
                {
                    new Label { Text = "ApexPost Courier", FontSize = 24, FontAttributes = FontAttributes.Bold },
                    status,
                    new Label { Text = "Development sign-in (API must run in Development)" },
                    courierId,
                    devToken,
                    pastedToken,
                    usePasted,
                    signOut
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task GetDevelopmentTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(courierId.Text))
        {
            status.Text = "Enter your courier ID.";
            return;
        }

        try
        {
            var token = await api().RequestDevelopmentTokenAsync(courierId.Text.Trim());
            await SaveAsync(token.AccessToken);
        }
        catch (ApiException exception)
        {
            status.Text = exception.UserMessage;
        }
    }

    private async Task SaveAsync(string? token)
    {
        var info = JwtClaimsReader.TryRead(token?.Trim());
        if (info is null || string.IsNullOrWhiteSpace(info.CourierId) || !info.Roles.Contains("Courier"))
        {
            status.Text = "That is not a Courier token with a courierId claim.";
            return;
        }

        if (info.IsExpired(DateTimeOffset.UtcNow))
        {
            status.Text = "That token has already expired.";
            return;
        }

        await tokens.SaveAccessTokenAsync(token!.Trim());
        pastedToken.Text = string.Empty;
        await RefreshAsync();
        await Shell.Current.GoToAsync("//newscan");
    }

    private async Task RefreshAsync()
    {
        var info = JwtClaimsReader.TryRead(await tokens.GetAccessTokenAsync());
        status.Text = info?.CourierId is { } id
            ? $"Signed in as {id} (token expires {info.ExpiresAtUtc:HH:mm} UTC)"
            : "Not signed in.";
    }
}
