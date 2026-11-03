using FitnessTracker.Domain.BodyMeasurements;

namespace FitnessTracker.Application.BodyMeasurements;

/// <summary>Ukládá samostatnou časovou řadu obvodu pasu.</summary>
public interface IWaistCircumferenceMeasurementRepository
{
    Task AddAsync(WaistCircumferenceMeasurement measurement);
    Task<IReadOnlyList<WaistCircumferenceMeasurement>> GetAllAsync();
    Task DeleteAsync(Guid measurementId);
}
