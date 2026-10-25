namespace FitnessTracker.Domain.WorkoutTemplates;

/// <summary>Plán názvu a pořadí cviků; neobsahuje skutečně provedené série.</summary>
public sealed class WorkoutTemplate
{
    public Guid Id { get; }
    public string Name { get; }
    public IReadOnlyList<WorkoutTemplateExercise> Exercises { get; }

    public WorkoutTemplate(string name, IEnumerable<Guid> exerciseIds)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Název šablony nesmí být prázdný.", nameof(name));
        Id = Guid.NewGuid();
        Name = name.Trim();
        Exercises = ValidateExercises(Id, CreateExercises(Id, exerciseIds));
    }

    private WorkoutTemplate(Guid id, string name, IEnumerable<WorkoutTemplateExercise> exercises)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("ID šablony nesmí být prázdné.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Název šablony nesmí být prázdný.", nameof(name));

        Id = id;
        Name = name.Trim();
        Exercises = ValidateExercises(id, exercises);
    }

    private static IReadOnlyList<WorkoutTemplateExercise> ValidateExercises(Guid id,
        IEnumerable<WorkoutTemplateExercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        var items = exercises.OrderBy(item => item.Position).ToList();
        if (items.Count == 0)
            throw new ArgumentException("Šablona musí obsahovat alespoň jeden cvik.", nameof(exercises));
        if (items.Select(item => item.ExerciseId).Distinct().Count() != items.Count)
            throw new ArgumentException("Stejný cvik nelze do šablony přidat dvakrát.", nameof(exercises));
        if (items.Where((item, index) => item.TemplateId != id || item.Position != index + 1).Any())
            throw new ArgumentException("Položky šablony musí mít správnou identitu a souvislé pořadí.", nameof(exercises));
        return items.AsReadOnly();
    }

    public static WorkoutTemplate Restore(Guid id, string name, IEnumerable<WorkoutTemplateExercise> exercises) =>
        new(id, name, exercises);

    public WorkoutTemplate WithContents(string name, IEnumerable<Guid> exerciseIds) =>
        new(Id, name, CreateExercises(Id, exerciseIds));

    private static IEnumerable<WorkoutTemplateExercise> CreateExercises(Guid templateId, IEnumerable<Guid> exerciseIds)
    {
        ArgumentNullException.ThrowIfNull(exerciseIds);
        return exerciseIds.Select((exerciseId, index) =>
            new WorkoutTemplateExercise(Guid.NewGuid(), templateId, exerciseId, index + 1)).ToList();
    }
}
