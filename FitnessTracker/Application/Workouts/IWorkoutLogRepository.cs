using FitnessTracker.Domain.Workouts;

namespace FitnessTracker.Application.Workouts;

public interface IWorkoutLogRepository
{
    /// <summary>Vrátí celý trénink s cviky a sériemi, nebo null pro nenalezený trénink.</summary>
    Task<WorkoutLog?> GetAsync(Guid workoutId);
    /// <summary>Přidá aktivní katalogový cvik na konec tréninku, název zkopíruje do historie.</summary>
    Task AddExerciseAsync(Guid workoutId, Guid exerciseId);
    Task AddSetAsync(Guid workoutExerciseId, int repetitions, long? loadGrams);
    /// <summary>Opraví opakování a zátěž série, a to i v ukončeném tréninku.</summary>
    Task UpdateSetAsync(Guid setId, int repetitions, long? loadGrams);
    /// <summary>Směr je -1 nebo 1; pohyb za okraj je beze změny. Pozice se mění atomicky.</summary>
    Task MoveExerciseAsync(Guid workoutExerciseId, int direction);
    Task MoveSetAsync(Guid setId, int direction);
    /// <summary>Ukončí trénink. Opakování zachová původní konec; další záznam už není povolen.</summary>
    Task FinishAsync(Guid workoutId, DateTimeOffset finishedAt);
}

// Předvídatelné porušení pravidla lze v UI vysvětlit bez zobrazení technické SQLite chyby.
public sealed class WorkoutRecordingException(string message) : InvalidOperationException(message);
