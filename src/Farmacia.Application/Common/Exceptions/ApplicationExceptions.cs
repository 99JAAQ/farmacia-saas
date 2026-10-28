namespace Farmacia.Application.Common.Exceptions;

// Use-case errors. The API maps each one to an HTTP status; messages are user-facing (Spanish).
// Business rule violations inside the model are Domain.Common.DomainException (400).

/// <summary>The request is not valid for this use case (400).</summary>
public class ValidationException(string message) : Exception(message);

/// <summary>Authentication failed: wrong credentials, locked account or invalid session (401).</summary>
public class UnauthorizedException(string message) : Exception(message);

/// <summary>
/// The user may see the resource but not take this action on it, e.g. closing another cashier's shift (403).
/// A resource of another pharmacy is a 404, never this.
/// </summary>
public class ForbiddenException(string message) : Exception(message);

/// <summary>The requested resource does not exist for the current pharmacy (404).</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>The request clashes with existing data, e.g. a barcode already in use (409).</summary>
public class ConflictException(string message, Exception? innerException = null) : Exception(message, innerException);
