namespace Farmacia.Application.Abstractions;

/// <summary>
/// The current time in the pharmacy's local time (Colombia, UTC-5). Use cases take "now" from here,
/// never from the client: nobody can register a reception with a made-up date.
/// </summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}
