namespace FitnessTracker.Application.Statistics;

/// <summary>Souhrn aktuálně uložených tréninků a sérií bez filtrování podle období.</summary>
public sealed record WorkoutStatisticsSnapshot(
    int WorkoutCount,
    int FinishedWorkoutCount,
    int SetCount,
    decimal VolumeKilogramRepetitions);

/// <summary>Načítá statistikám potřebná data nezávisle na konkrétním úložišti.</summary>
public interface IWorkoutStatisticsRepository
{
    Task<WorkoutStatisticsSnapshot> GetAllTimeAsync();
}
