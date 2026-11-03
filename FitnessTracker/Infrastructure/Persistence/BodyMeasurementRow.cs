using System.Globalization;
using FitnessTracker.Domain.BodyMeasurements;

namespace FitnessTracker.Infrastructure.Persistence;

internal sealed class BodyMeasurementRow
{
    public string Id { get; set; } = string.Empty;
    public string BodyPart { get; set; } = string.Empty;
    public string MeasuredAtUtc { get; set; } = string.Empty;
    public long CircumferenceMillimeters { get; set; }

    public BodyMeasurement ToMeasurement() => BodyMeasurement.Restore(
        Guid.ParseExact(Id, "D"),
        Enum.Parse<FitnessTracker.Domain.BodyMeasurements.BodyPart>(BodyPart, ignoreCase: false),
        DateTimeOffset.ParseExact(MeasuredAtUtc, "O", CultureInfo.InvariantCulture, DateTimeStyles.None),
        CircumferenceMillimeters);

    public static BodyMeasurementRow FromMeasurement(BodyMeasurement measurement) => new()
    {
        Id = measurement.Id.ToString("D"),
        BodyPart = measurement.BodyPart.ToString(),
        MeasuredAtUtc = measurement.MeasuredAtUtc.ToString("O", CultureInfo.InvariantCulture),
        CircumferenceMillimeters = measurement.CircumferenceMillimeters
    };
}
