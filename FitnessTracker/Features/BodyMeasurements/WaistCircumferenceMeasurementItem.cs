using System.Globalization;
using FitnessTracker.Domain.BodyMeasurements;

namespace FitnessTracker.Features.BodyMeasurements;

/// <summary>Formátuje milimetry a čas pro seznam, aniž by měnil uložená data.</summary>
public sealed class WaistCircumferenceMeasurementItem(WaistCircumferenceMeasurement measurement)
{
    public WaistCircumferenceMeasurement Measurement { get; } = measurement;
    public string CircumferenceText =>
        $"{(Measurement.CircumferenceMillimeters / 10m).ToString("0.#", CultureInfo.CurrentCulture)} cm";
    public string MeasuredAtText => Measurement.MeasuredAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
}
