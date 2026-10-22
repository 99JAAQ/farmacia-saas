using Farmacia.Domain.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Farmacia.Infrastructure.Persistence.Converters;

/// <summary>Stores a <see cref="Nit"/> as its normalized text, e.g. "900123456-8".</summary>
public class NitConverter() : ValueConverter<Nit, string>(nit => nit.Value, value => Nit.Parse(value));
