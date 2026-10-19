using FitnessTracker.Domain.Exercises;

namespace FitnessTracker.Application.Exercises;

public interface IExerciseRepository
{
    /// <summary>Přidá cvik; null odmítne ArgumentNullException, duplicitní ID InvalidOperationException.</summary>
    Task AddAsync(Exercise exercise);

    /// <summary>
    /// Vrátí snímek pouze pro čtení. Výchozí nabídka skrývá archivované cviky.
    /// Řadí podle názvu pomocí OrdinalIgnoreCase, při shodě podle ID. Prázdný výsledek není null.
    /// </summary>
    Task<IReadOnlyList<Exercise>> GetAllAsync(bool includeArchived = false);

    /// <summary>Vrátí cvik včetně archivovaného, nebo null při nenalezeném ID.</summary>
    Task<Exercise?> GetByIdAsync(Guid id);

    /// <summary>
    /// Nahradí údaje existujícího cviku při zachování ID. Nenalezené ID vyvolá KeyNotFoundException.
    /// Null vyvolá ArgumentNullException. Nový záznam tato operace nikdy nevytváří.
    /// </summary>
    Task UpdateAsync(Exercise exercise);
}
