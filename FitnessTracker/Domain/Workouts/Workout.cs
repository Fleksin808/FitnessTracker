using System;

namespace FitnessTracker.Domain.Workouts;

// Model popisuje jeden trénink a jeho pravidla nezávisle na obrazovce nebo databázi.
public sealed class Workout
{
    public Guid Id { get; }
    public string Name { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? FinishedAt { get; }
    public bool IsFinished => FinishedAt.HasValue;

    public Workout(string name, DateTimeOffset startedAt)
        : this(Guid.NewGuid(), name, startedAt)
    {
    }

    private Workout(Guid id, string name, DateTimeOffset startedAt, DateTimeOffset? finishedAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("ID tréninku nesmí být prázdné.", nameof(id));

        // Pravidlo hlídá model, takže platí i při vytvoření mimo formulář.
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Název tréninku nesmí být prázdný.", nameof(name));

        // Nový trénink dostane nové ID; při načtení zachováme původní identitu.
        Id = id;
        Name = name.Trim();

        // Čas předává volající; model se nespoléhá na aktuální čas zařízení.
        // UTC sjednotí zápis stejného okamžiku z různých časových pásem.
        StartedAt = startedAt.ToUniversalTime();
        if (finishedAt < startedAt)
            throw new ArgumentException("Konec tréninku nesmí předcházet začátku.", nameof(finishedAt));
        FinishedAt = finishedAt?.ToUniversalTime();
    }

    /// <summary>
    /// Obnoví uložený trénink se stejným ID a se stejnými pravidly platnosti jako nový trénink.
    /// </summary>
    public static Workout Restore(Guid id, string name, DateTimeOffset startedAt, DateTimeOffset? finishedAt = null)
    {
        return new Workout(id, name, startedAt, finishedAt);
    }

    // Ukončení ponechává identitu i obsah; opakované ukončení zachová původní čas konce.
    public Workout Finish(DateTimeOffset finishedAt) => IsFinished ? this : new(Id, Name, StartedAt, finishedAt);
}
