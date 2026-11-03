using System.Globalization;
using FitnessTracker.Domain.BodyMeasurements;

namespace FitnessTracker.Infrastructure.Persistence;

internal sealed class WaistCircumferenceMeasurementRow
{
    public string Id { get; set; } = string.Empty;
    public string MeasuredAtUtc { get; set; } = string.Empty;
    public long CircumferenceMillimeters { get; set; }

    public WaistCircumferenceMeasurement ToMeasurement() => WaistCircumferenceMeasurement.Restore(
        Guid.ParseExact(Id, "D"),
        DateTimeOffset.ParseExact(MeasuredAtUtc, "O", CultureInfo.InvariantCulture, DateTimeStyles.None),
        CircumferenceMillimeters);

    public static WaistCircumferenceMeasurementRow FromMeasurement(WaistCircumferenceMeasurement measurement) => new()
    {
        Id = measurement.Id.ToString("D"),
        MeasuredAtUtc = measurement.MeasuredAtUtc.ToString("O", CultureInfo.InvariantCulture),
        CircumferenceMillimeters = measurement.CircumferenceMillimeters
    };
}
