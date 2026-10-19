using FitnessTracker.Domain.Exercises;
using SQLite;

namespace FitnessTracker.Infrastructure.Persistence;

[Table("Exercises")]
internal sealed class ExerciseRow
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsArchived { get; set; }

    public static ExerciseRow FromExercise(Exercise exercise) => new()
    {
        Id = exercise.Id.ToString("D"),
        Name = exercise.Name,
        IsArchived = exercise.IsArchived
    };

    public Exercise ToExercise() => Exercise.Restore(Guid.ParseExact(Id, "D"), Name, IsArchived);
}
