using FitnessTracker.Domain.BodyMeasurements;

namespace FitnessTracker.Application.BodyMeasurements;

/// <summary>Ukládá a načítá měření tělesné hmotnosti bez vazby na obrazovku nebo databázi.</summary>
public interface IBodyWeightMeasurementRepository
{
    /// <summary>Přidá měření; duplicitní ID odmítne a nikdy nepřepíše existující záznam.</summary>
    Task AddAsync(BodyWeightMeasurement measurement);

    /// <summary>Vrátí všechna měření od nejnovějšího; shodný čas řadí podle ID.</summary>
    Task<IReadOnlyList<BodyWeightMeasurement>> GetAllAsync();

    /// <summary>Smaže měření podle ID; při jeho nenalezení vyvolá KeyNotFoundException.</summary>
    Task DeleteAsync(Guid measurementId);
}
