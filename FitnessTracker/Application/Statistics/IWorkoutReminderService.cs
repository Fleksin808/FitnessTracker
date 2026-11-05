namespace FitnessTracker.Application.Statistics;

/// <summary>Odděluje nastavení tréninkové připomínky od konkrétního API telefonu.</summary>
public interface IWorkoutReminderService
{
    bool IsSupported { get; }
    Task<bool> RequestPermissionAsync();
    Task ScheduleWeeklyAsync(DayOfWeek day, TimeSpan localTime);
    Task CancelAsync();
}
