using System.Globalization;
using FitnessTracker.Domain.BodyMeasurements;

namespace FitnessTracker.Features.BodyMeasurements;

/// <summary>Prezentace odděluje formát času a jednotek od uloženého doménového modelu.</summary>
public sealed class BodyWeightMeasurementItem(BodyWeightMeasurement measurement)
{
    public BodyWeightMeasurement Measurement { get; } = measurement;
    public string WeightText => $"{(Measurement.WeightGrams / 1000m).ToString("0.###", CultureInfo.CurrentCulture)} kg";
    public string MeasuredAtText => Measurement.MeasuredAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
}
