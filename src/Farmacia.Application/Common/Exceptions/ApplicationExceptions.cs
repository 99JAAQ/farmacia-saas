namespace Farmacia.Application.Common.Exceptions;

// Use-case errors. The API maps each one to an HTTP status; messages are user-facing (Spanish).
// Business rule violations inside the model are Domain.Common.DomainException (400).

/// <summary>The request is not valid for this use case (400).</summary>
public class ValidationException(string message) : Exception(message);

/// <summary>The requested resource does not exist for the current pharmacy (404).</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>The request clashes with existing data, e.g. a barcode already in use (409).</summary>
public class ConflictException(string message, Exception? innerException = null) : Exception(message, innerException);
