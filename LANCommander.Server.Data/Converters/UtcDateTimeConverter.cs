using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LANCommander.Server.Data.Converters;

/// <summary>
/// Stores every DateTime as UTC. PostgreSQL's "timestamp with time zone" columns only accept UTC
/// values, but dates often arrive without a kind: read from an import manifest, picked in a date
/// field, or written as a literal. Those are already UTC in practice (the server writes UTC), so an
/// unspecified value is taken as UTC and a local one converted. Values read back are UTC.
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Utc
        ? value
        : value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
