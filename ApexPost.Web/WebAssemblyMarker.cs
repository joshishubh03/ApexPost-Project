namespace ApexPost.Web;

/// <summary>
/// Entry-assembly marker for WebApplicationFactory&lt;T&gt; in ApexPost.IntegrationTests. That project hosts both
/// the MVC app and ApexPost.Api, and both have a global <c>Program</c> class, so tests name the assembly through
/// this type instead.
/// </summary>
public sealed class WebAssemblyMarker;
