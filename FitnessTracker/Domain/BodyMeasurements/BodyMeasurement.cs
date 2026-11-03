namespace FitnessTracker.Domain.BodyMeasurements;

/// <summary>Jeden časově označený obvod vybrané části těla uložený v celých milimetrech.</summary>
public sealed class BodyMeasurement
{
    public Guid Id { get; }
    public BodyPart BodyPart { get; }
    public DateTimeOffset MeasuredAtUtc { get; }
    public long CircumferenceMillimeters { get; }

    public BodyMeasurement(BodyPart bodyPart, DateTimeOffset measuredAt, long circumferenceMillimeters)
        : this(Guid.NewGuid(), bodyPart, measuredAt, circumferenceMillimeters) { }

    private BodyMeasurement(Guid id, BodyPart bodyPart, DateTimeOffset measuredAt, long circumferenceMillimeters)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("ID měření nesmí být prázdné.", nameof(id));
        if (!Enum.IsDefined(bodyPart))
            throw new ArgumentOutOfRangeException(nameof(bodyPart), "Část těla není podporována.");
        if (circumferenceMillimeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(circumferenceMillimeters), "Obvod musí být kladný.");

        Id = id;
        BodyPart = bodyPart;
        MeasuredAtUtc = measuredAt.ToUniversalTime();
        CircumferenceMillimeters = circumferenceMillimeters;
    }

    public static BodyMeasurement Restore(Guid id, BodyPart bodyPart, DateTimeOffset measuredAt,
        long circumferenceMillimeters) => new(id, bodyPart, measuredAt, circumferenceMillimeters);
}
