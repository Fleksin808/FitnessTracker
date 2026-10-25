using FitnessTracker.Domain.WorkoutTemplates;

namespace FitnessTracker.Infrastructure.Persistence;

internal sealed class WorkoutTemplateRow
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public WorkoutTemplate ToTemplate(IEnumerable<WorkoutTemplateExerciseRow> exercises) =>
        WorkoutTemplate.Restore(Guid.ParseExact(Id, "D"), Name, exercises.Select(row => row.ToExercise()));
}

internal sealed class WorkoutTemplateExerciseRow
{
    public string Id { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string ExerciseId { get; set; } = string.Empty;
    public int Position { get; set; }
    public WorkoutTemplateExercise ToExercise() => new(Guid.ParseExact(Id, "D"), Guid.ParseExact(TemplateId, "D"),
        Guid.ParseExact(ExerciseId, "D"), Position);
}
