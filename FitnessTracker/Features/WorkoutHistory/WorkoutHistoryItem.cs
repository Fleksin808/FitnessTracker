using FitnessTracker.Domain.Workouts;

namespace FitnessTracker.Features.WorkoutHistory;

/// <summary>Údaje tréninku připravené pro jeden řádek historie.</summary>
public sealed class WorkoutHistoryItem
{
    public WorkoutHistoryItem(Workout workout)
    {
        Workout = workout;
        Name = workout.Name;
        // Data filtru i popisek používají místní kalendářní datum, nikoliv UTC den.
        StartedAtText = workout.StartedAt.ToLocalTime().ToString("g");
        StatusText = workout.IsFinished ? "Ukončeno" : "Rozpracováno";
    }

    public Workout Workout { get; }
    public string Name { get; }
    public string StartedAtText { get; }
    public string StatusText { get; }
}
