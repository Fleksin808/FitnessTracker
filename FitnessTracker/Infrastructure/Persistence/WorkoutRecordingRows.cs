using FitnessTracker.Domain.Workouts;

namespace FitnessTracker.Infrastructure.Persistence;

internal sealed class WorkoutExerciseRow
{
    public string Id { get; set; } = string.Empty;
    public string WorkoutId { get; set; } = string.Empty;
    public string ExerciseId { get; set; } = string.Empty;
    public string ExerciseNameSnapshot { get; set; } = string.Empty;
    public int Position { get; set; }
    public WorkoutExercise ToExercise() => new(Guid.ParseExact(Id, "D"), Guid.ParseExact(WorkoutId, "D"),
        Guid.ParseExact(ExerciseId, "D"), ExerciseNameSnapshot, Position);
}

internal sealed class WorkoutSetRow
{
    public string Id { get; set; } = string.Empty;
    public string WorkoutExerciseId { get; set; } = string.Empty;
    public int Position { get; set; }
    public int Repetitions { get; set; }
    public long? LoadGrams { get; set; }
    public WorkoutSet ToSet() => new(Guid.ParseExact(Id, "D"), Guid.ParseExact(WorkoutExerciseId, "D"),
        Position, Repetitions, LoadGrams);
}
