namespace FitnessTracker.Domain.BodyMeasurements;

/// <summary>Jeden časově označený záznam obvodu pasu v celých milimetrech.</summary>
public sealed class WaistCircumferenceMeasurement
{
    public Guid Id { get; }
    public DateTimeOffset MeasuredAtUtc { get; }
    public long CircumferenceMillimeters { get; }

    public WaistCircumferenceMeasurement(DateTimeOffset measuredAt, long circumferenceMillimeters)
        : this(Guid.NewGuid(), measuredAt, circumferenceMillimeters) { }

    private WaistCircumferenceMeasurement(Guid id, DateTimeOffset measuredAt, long circumferenceMillimeters)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("ID měření nesmí být prázdné.", nameof(id));
        if (circumferenceMillimeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(circumferenceMillimeters), "Obvod pasu musí být kladný.");

        Id = id;
        MeasuredAtUtc = measuredAt.ToUniversalTime();
        CircumferenceMillimeters = circumferenceMillimeters;
    }

    public static WaistCircumferenceMeasurement Restore(Guid id, DateTimeOffset measuredAt,
        long circumferenceMillimeters) => new(id, measuredAt, circumferenceMillimeters);
}
