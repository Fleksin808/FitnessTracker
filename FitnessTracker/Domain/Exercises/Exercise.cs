namespace FitnessTracker.Domain.Exercises;

// Definice cviku v katalogu; opakování a zátěž budou patřit až konkrétní sérii v tréninku.
public sealed class Exercise
{
    public Guid Id { get; }
    public string Name { get; }
    public bool IsArchived { get; }

    public Exercise(string name) : this(Guid.NewGuid(), name, false) { }

    private Exercise(Guid id, string name, bool isArchived)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("ID cviku nesmí být prázdné.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Název cviku nesmí být prázdný.", nameof(name));

        Id = id;
        Name = name.Trim();
        IsArchived = isArchived;
    }

    public static Exercise Restore(Guid id, string name, bool isArchived) => new(id, name, isArchived);

    // Vracíme novou verzi stejného cviku. Původní objekt ani dříve načtený snímek neměníme.
    public Exercise WithName(string name) => new(Id, name, IsArchived);
    public Exercise WithArchived(bool isArchived) => new(Id, Name, isArchived);
}
