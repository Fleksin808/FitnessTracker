namespace FitnessTracker.Domain.BodyMeasurements;

/// <summary>Jeden okamžik měření tělesné hmotnosti, nezávislý na tréninku a jeho sériích.</summary>
public sealed class BodyWeightMeasurement
{
    public Guid Id { get; }
    public DateTimeOffset MeasuredAtUtc { get; }
    public long WeightGrams { get; }

    public BodyWeightMeasurement(DateTimeOffset measuredAt, long weightGrams)
        : this(Guid.NewGuid(), measuredAt, weightGrams) { }

    private BodyWeightMeasurement(Guid id, DateTimeOffset measuredAt, long weightGrams)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("ID měření nesmí být prázdné.", nameof(id));
        if (weightGrams <= 0)
            throw new ArgumentOutOfRangeException(nameof(weightGrams), "Hmotnost musí být kladná.");

        Id = id;
        // Čas normalizujeme hned v doméně, aby se do úložiště nedostalo místní pásmo.
        MeasuredAtUtc = measuredAt.ToUniversalTime();
        WeightGrams = weightGrams;
    }

    public static BodyWeightMeasurement Restore(Guid id, DateTimeOffset measuredAt, long weightGrams) =>
        new(id, measuredAt, weightGrams);
}
