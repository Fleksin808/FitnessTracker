using FitnessTracker.Domain.Workouts;

namespace FitnessTracker.Features.Workouts;

// Formátování patří do prezentační vrstvy; databáze i doména pracují s celými gramy.
public sealed class WorkoutExerciseItem(WorkoutExerciseLog log)
{
    public WorkoutExercise Exercise { get; } = log.Exercise;
    public string DisplayName => $"{Exercise.Position}. {Exercise.ExerciseNameSnapshot}";
    public IReadOnlyList<WorkoutSetItem> Sets { get; } = log.Sets.Select(set => new WorkoutSetItem(set)).ToList().AsReadOnly();
}

public sealed class WorkoutSetItem(WorkoutSet set)
{
    public WorkoutSet Set { get; } = set;
    public string DisplayText => Set.LoadGrams is { } grams
        ? $"{Set.Position}. série: {Set.Repetitions} opakování × {grams / 1000m:0.###} kg"
        : $"{Set.Position}. série: {Set.Repetitions} opakování, zátěž nezadaná";
}
