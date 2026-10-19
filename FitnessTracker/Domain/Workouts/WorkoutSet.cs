namespace FitnessTracker.Domain.Workouts;

public sealed class WorkoutSet
{
    public Guid Id { get; }
    public Guid WorkoutExerciseId { get; }
    public int Position { get; }
    public int Repetitions { get; }
    public long? LoadGrams { get; }

    public WorkoutSet(Guid id, Guid workoutExerciseId, int position, int repetitions, long? loadGrams)
    {
        if (id == Guid.Empty || workoutExerciseId == Guid.Empty)
            throw new ArgumentException("Série musí mít platná ID.");
        if (position < 1)
            throw new ArgumentOutOfRangeException(nameof(position));
        if (repetitions < 1)
            throw new ArgumentOutOfRangeException(nameof(repetitions), "Počet opakování musí být kladný.");
        if (loadGrams < 0)
            throw new ArgumentOutOfRangeException(nameof(loadGrams), "Zátěž nesmí být záporná.");
        Id = id;
        WorkoutExerciseId = workoutExerciseId;
        Position = position;
        Repetitions = repetitions;
        // null znamená nezadanou zátěž; 0 výslovně bez externí zátěže.
        LoadGrams = loadGrams;
    }
}
