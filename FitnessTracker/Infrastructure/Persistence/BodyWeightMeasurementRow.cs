using System.Globalization;
using FitnessTracker.Domain.BodyMeasurements;

namespace FitnessTracker.Infrastructure.Persistence;

internal sealed class BodyWeightMeasurementRow
{
    public string Id { get; set; } = string.Empty;
    public string MeasuredAtUtc { get; set; } = string.Empty;
    public long WeightGrams { get; set; }

    public BodyWeightMeasurement ToMeasurement() => BodyWeightMeasurement.Restore(
        Guid.ParseExact(Id, "D"),
        DateTimeOffset.ParseExact(MeasuredAtUtc, "O", CultureInfo.InvariantCulture, DateTimeStyles.None),
        WeightGrams);

    public static BodyWeightMeasurementRow FromMeasurement(BodyWeightMeasurement measurement) => new()
    {
        Id = measurement.Id.ToString("D"),
        MeasuredAtUtc = measurement.MeasuredAtUtc.ToString("O", CultureInfo.InvariantCulture),
        WeightGrams = measurement.WeightGrams
    };
}
