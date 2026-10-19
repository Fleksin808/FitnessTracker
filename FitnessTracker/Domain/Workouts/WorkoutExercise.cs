namespace FitnessTracker.Domain.Workouts;

public sealed class WorkoutExercise
{
    public Guid Id { get; }
    public Guid WorkoutId { get; }
    public Guid ExerciseId { get; }
    public string ExerciseNameSnapshot { get; }
    public int Position { get; }

    public WorkoutExercise(Guid id, Guid workoutId, Guid exerciseId, string exerciseNameSnapshot, int position)
    {
        if (id == Guid.Empty || workoutId == Guid.Empty || exerciseId == Guid.Empty)
            throw new ArgumentException("Záznam cviku musí mít platná ID.");
        if (string.IsNullOrWhiteSpace(exerciseNameSnapshot))
            throw new ArgumentException("Název zaznamenaného cviku nesmí být prázdný.");
        if (position < 1)
            throw new ArgumentOutOfRangeException(nameof(position));
        Id = id;
        WorkoutId = workoutId;
        ExerciseId = exerciseId;
        // Název je součást historie a nemění se při přejmenování katalogové definice.
        ExerciseNameSnapshot = exerciseNameSnapshot.Trim();
        Position = position;
    }
}
