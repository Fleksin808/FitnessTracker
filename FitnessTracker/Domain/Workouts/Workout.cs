using System;

namespace FitnessTracker.Domain.Workouts;

// Model popisuje jeden trénink a jeho pravidla nezávisle na obrazovce nebo databázi.
public sealed class Workout
{
    public Guid Id { get; }
    public string Name { get; }
    public DateTimeOffset StartedAt { get; }

    public Workout(string name, DateTimeOffset startedAt)
    {
        // Pravidlo hlídá model, takže platí i při vytvoření mimo formulář.
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Název tréninku nesmí být prázdný.", nameof(name));

        // ID odlišuje jednotlivé tréninky, i když mají stejný název.
        Id = Guid.NewGuid();
        Name = name.Trim();

        // Čas předává volající; model se nespoléhá na aktuální čas zařízení.
        // UTC sjednotí zápis stejného okamžiku z různých časových pásem.
        StartedAt = startedAt.ToUniversalTime();
    }
}
