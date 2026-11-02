namespace ApexPost.Domain.ValueObjects;

/// <summary>
/// Identity documents are never stored in full. Only a mask of at least four '*' characters followed by
/// at most the last four characters is kept (maximum 16 characters, matching the varchar(16) column).
/// </summary>
public static class IdentityDocumentMask
{
    public const int MaxLength = 16;
    public const int VisibleCharacters = 4;

    public static string Mask(string documentIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentIdentifier);
        var normalized = documentIdentifier.Trim();
        var visible = Math.Min(VisibleCharacters, normalized.Length);
        var maskLength = Math.Clamp(normalized.Length - visible, VisibleCharacters, MaxLength - VisibleCharacters);
        return new string('*', maskLength) + normalized[^visible..];
    }

    public static bool IsMasked(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaxLength &&
        value.TakeWhile(character => character == '*').Count() >= VisibleCharacters &&
        value.TrimStart('*').Length <= VisibleCharacters &&
        !value.TrimStart('*').Contains('*');

    public static void EnsureMasked(string value, string parameterName)
    {
        if (!IsMasked(value))
        {
            throw new ArgumentException(
                "Identity documents must be stored masked: at least four '*' followed by no more than the last four characters.",
                parameterName);
        }
    }
}
