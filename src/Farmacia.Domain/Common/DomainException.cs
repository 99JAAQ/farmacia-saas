namespace Farmacia.Domain.Common;

/// <summary>A business rule was violated. Messages are user-facing (Spanish).</summary>
public class DomainException(string message) : Exception(message);
