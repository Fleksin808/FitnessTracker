using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitnessTracker.Domain.Workouts;

namespace FitnessTracker.Application.Workouts;

/// <summary>
/// Dohoda pro ukládání a načítání tréninků bez znalosti konkrétního úložiště.
/// </summary>
public interface IWorkoutRepository
{
    /// <summary>
    /// Přidá nový trénink. Existující trénink se stejným ID nesmí přepsat.
    /// </summary>
    /// <exception cref="ArgumentNullException">Trénink je null.</exception>
    /// <exception cref="InvalidOperationException">Trénink se stejným ID už existuje.</exception>
    // Task umožní později čekat na databázi pomocí await bez změny rozhraní.
    Task AddAsync(Workout workout);

    /// <summary>
    /// Vrátí snímek seznamu od nejnovějšího začátku; při shodě rozhoduje ID vzestupně.
    /// Prázdné úložiště vrací prázdný seznam, nikoliv null.
    /// </summary>
    // Volající seznam čte; přidání tréninku musí provést přes AddAsync.
    Task<IReadOnlyList<Workout>> GetAllAsync();

    /// <summary>
    /// Najde trénink podle ID. Pokud neexistuje, vrátí null.
    /// Chyba úložiště se hlásí výjimkou, nikoliv návratem null.
    /// </summary>
    // Otazník výslovně říká, že hledání nemusí najít žádný záznam.
    Task<Workout?> GetByIdAsync(Guid id);
}
