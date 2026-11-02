namespace ApexPost.Web.Models;

/// <summary>Shown with HTTP 409 when a concurrency conflict (rowversion) rejected a change.</summary>
public sealed record ConflictViewModel(string Message, string ReturnUrl);
