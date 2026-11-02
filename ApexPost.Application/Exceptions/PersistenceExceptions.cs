namespace ApexPost.Application.Exceptions;

/// <summary>
/// Raised by the unit of work when another user changed or removed the same row (rowversion mismatch).
/// The Web layer maps it to HTTP 409 Conflict.
/// </summary>
public sealed class ConcurrencyConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>Raised when a UNIQUE constraint rejects a write (for example a duplicate SKU or branch code). Mapped to HTTP 409.</summary>
public sealed class DuplicateResourceException(string message, Exception? innerException = null)
    : Exception(message, innerException);
