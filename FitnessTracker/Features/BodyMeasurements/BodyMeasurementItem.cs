using System.Globalization;
using FitnessTracker.Domain.BodyMeasurements;

namespace FitnessTracker.Features.BodyMeasurements;

/// <summary>Formátuje uložený záznam pro přehled všech měřených částí těla.</summary>
public sealed class BodyMeasurementItem(BodyMeasurement measurement)
{
    public BodyMeasurement Measurement { get; } = measurement;
    public string BodyPartName => BodyPartOption.All.First(item => item.Value == Measurement.BodyPart).Name;
    public string CircumferenceText =>
        $"{(Measurement.CircumferenceMillimeters / 10m).ToString("0.#", CultureInfo.CurrentCulture)} cm";
    public string MeasuredAtText => Measurement.MeasuredAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
}
