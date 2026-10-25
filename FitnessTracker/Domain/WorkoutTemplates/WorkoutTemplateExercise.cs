namespace FitnessTracker.Domain.WorkoutTemplates;

public sealed class WorkoutTemplateExercise
{
    public Guid Id { get; }
    public Guid TemplateId { get; }
    public Guid ExerciseId { get; }
    public int Position { get; }

    public WorkoutTemplateExercise(Guid id, Guid templateId, Guid exerciseId, int position)
    {
        if (id == Guid.Empty || templateId == Guid.Empty || exerciseId == Guid.Empty)
            throw new ArgumentException("Položka šablony musí mít platná ID.");
        if (position < 1)
            throw new ArgumentOutOfRangeException(nameof(position));
        Id = id;
        TemplateId = templateId;
        ExerciseId = exerciseId;
        Position = position;
    }
}
