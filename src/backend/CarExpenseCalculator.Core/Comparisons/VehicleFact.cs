using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Core.Comparisons;

public enum VehicleFactState { Known, Unknown, NotApplicable, Conflicting }

// The metadata describes this observation, not a transferable verification of a field.
// Dates absent from older listing contracts stay null. Core never supplies a clock.
public sealed record ComparisonEvidence(
    FieldOrigin Origin,
    ExtractionMethod ExtractionMethod,
    VerificationStatus Verification,
    ListingUrl? SourceUrl = null,
    DateTimeOffset? ObservedAt = null,
    DateTimeOffset? ConfirmedAt = null);

public sealed class FactObservation<T>(T value, ComparisonEvidence evidence) where T : notnull
{
    public T Value { get; } = value;
    public ComparisonEvidence Evidence { get; } = evidence;
}

// Supported values are immutable scalars and FuelTypeSet. Construction preserves input
// errors for the processor; callers must normalize before treating a fact set as valid.
public sealed class VehicleFact<T> where T : notnull
{
    public VehicleFact(VehicleFactState state, IEnumerable<FactObservation<T>>? observations = null)
    {
        State = state;
        Observations = Array.AsReadOnly(observations?.ToArray() ?? []);
    }

    public VehicleFactState State { get; }
    public IReadOnlyList<FactObservation<T>> Observations { get; }

    public static VehicleFact<T> Unknown() => new(VehicleFactState.Unknown);
    public static VehicleFact<T> NotApplicable() => new(VehicleFactState.NotApplicable);
    public static VehicleFact<T> Known(T value, ComparisonEvidence evidence) =>
        new(VehicleFactState.Known, [new(value, evidence)]);
    public static VehicleFact<T> Conflicting(IEnumerable<FactObservation<T>> observations) =>
        new(VehicleFactState.Conflicting, observations);

    // Replacement keeps no previous observations, source verification, or timestamps.
    public VehicleFact<T> ReplaceWithManual(T value, DateTimeOffset confirmedAt, DateTimeOffset? observedAt = null) =>
        Known(value, new(FieldOrigin.User, ExtractionMethod.Manual,
            VerificationStatus.UserConfirmed, ObservedAt: observedAt, ConfirmedAt: confirmedAt));

    public VehicleFact<T> ResolveWithManual(T value, DateTimeOffset confirmedAt, DateTimeOffset? observedAt = null)
    {
        if (State != VehicleFactState.Conflicting)
        {
            throw new VehicleFactsValidationException([
                new("state", "invalidState", "Only conflicting facts can be explicitly resolved.")]);
        }

        return ReplaceWithManual(value, confirmedAt, observedAt);
    }
}

public sealed class FuelTypeSet : IEquatable<FuelTypeSet>
{
    public FuelTypeSet(IEnumerable<FuelType> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Values = Array.AsReadOnly(values.ToArray());
    }

    public IReadOnlyList<FuelType> Values { get; }

    public bool Equals(FuelTypeSet? other) => other is not null && Values.ToHashSet().SetEquals(other.Values);
    public override bool Equals(object? obj) => obj is FuelTypeSet other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in Values.Distinct().Order())
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
}

public sealed record VehicleFactsValidationError(string Path, string Code, string Message);

public sealed class VehicleFactsValidationException : Exception
{
    public VehicleFactsValidationException(IEnumerable<VehicleFactsValidationError> errors)
        : base("Vehicle facts contain invalid supplied values or evidence.")
    {
        Errors = Array.AsReadOnly(errors.ToArray());
    }

    public IReadOnlyList<VehicleFactsValidationError> Errors { get; }
}
