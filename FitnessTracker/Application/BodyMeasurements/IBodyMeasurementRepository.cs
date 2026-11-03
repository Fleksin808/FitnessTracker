using FitnessTracker.Domain.BodyMeasurements;

namespace FitnessTracker.Application.BodyMeasurements;

/// <summary>Ukládá časovou řadu obvodů různých částí těla.</summary>
public interface IBodyMeasurementRepository
{
    Task AddAsync(BodyMeasurement measurement);
    Task<IReadOnlyList<BodyMeasurement>> GetAllAsync();
    Task DeleteAsync(Guid measurementId);
}
