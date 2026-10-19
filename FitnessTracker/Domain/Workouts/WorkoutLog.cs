namespace FitnessTracker.Domain.Workouts;

// Konzistentní snímek celého tréninku načtený v jedné databázové transakci.
public sealed class WorkoutLog
{
    public Workout Workout { get; }
    public IReadOnlyList<WorkoutExerciseLog> Exercises { get; }

    public WorkoutLog(Workout workout, IEnumerable<WorkoutExerciseLog> exercises)
    {
        Workout = workout;
        Exercises = exercises.ToList().AsReadOnly();
    }
}

public sealed class WorkoutExerciseLog
{
    public WorkoutExercise Exercise { get; }
    public IReadOnlyList<WorkoutSet> Sets { get; }

    public WorkoutExerciseLog(WorkoutExercise exercise, IEnumerable<WorkoutSet> sets)
    {
        Exercise = exercise;
        Sets = sets.ToList().AsReadOnly();
    }
}
