using System.Globalization;
using FitnessTracker.Domain.Workouts;
using SQLite;

namespace FitnessTracker.Infrastructure.Persistence;

// Databázový záznam má veřejné settery pro mapování SQLite; doménový model zůstává nezávislý.
[Table("Workouts")]
internal sealed class WorkoutRow
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string StartedAt { get; set; } = string.Empty;
    public string? FinishedAt { get; set; }

    public static WorkoutRow FromWorkout(Workout workout) => new()
    {
        Id = workout.Id.ToString("D"),
        Name = workout.Name,
        // Pevný UTC formát nezávisí na jazyce telefonu a zachová přesnost času.
        StartedAt = workout.StartedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        FinishedAt = workout.FinishedAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
    };

    public Workout ToWorkout()
    {
        // Poškozený záznam vyvolá chybu, nesmí se tvářit jako nenalezený trénink.
        return Workout.Restore(
            Guid.ParseExact(Id, "D"),
            Name,
            DateTimeOffset.ParseExact(StartedAt, "O", CultureInfo.InvariantCulture),
            FinishedAt is null ? null : DateTimeOffset.ParseExact(FinishedAt, "O", CultureInfo.InvariantCulture));
    }
}
