#if ANDROID || IOS
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
#endif
using FitnessTracker.Application.Statistics;

namespace FitnessTracker.Infrastructure.Notifications;

public sealed class LocalWorkoutReminderService : IWorkoutReminderService
{
    private const int NotificationId = 1101;

    public bool IsSupported
    {
        get
        {
#if ANDROID || IOS
            return true;
#else
            return false;
#endif
        }
    }

    public Task<bool> RequestPermissionAsync()
    {
#if ANDROID || IOS
        return RequestPermissionCoreAsync();
#else
        return Task.FromResult(false);
#endif
    }

    public Task ScheduleWeeklyAsync(DayOfWeek day, TimeSpan localTime)
    {
#if ANDROID || IOS
        DateTimeOffset now = DateTimeOffset.Now;
        int daysUntilReminder = ((int)day - (int)now.DayOfWeek + 7) % 7;
        DateTime localCandidate = now.LocalDateTime.Date.AddDays(daysUntilReminder).Add(localTime);
        if (localCandidate <= now.LocalDateTime) localCandidate = localCandidate.AddDays(7);
        DateTimeOffset firstOccurrence = new(localCandidate);

        var request = new NotificationRequest
        {
            NotificationId = NotificationId,
            Title = "FitnessTracker",
            Description = "Nezapomeň si zkontrolovat svůj týdenní cíl tréninků.",
            Schedule =
            {
                NotifyTime = firstOccurrence,
                RepeatType = NotificationRepeat.Weekly
            }
        };

        return ScheduleNotificationAsync(request);
#else
        return Task.CompletedTask;
#endif
    }

    public Task CancelAsync()
    {
#if ANDROID || IOS
        _ = LocalNotificationCenter.Current.Cancel(NotificationId);
        return Task.CompletedTask;
#else
        return Task.CompletedTask;
#endif
    }

#if ANDROID || IOS
    private static async Task ScheduleNotificationAsync(NotificationRequest request)
    {
        if (!await LocalNotificationCenter.Current.Show(request))
            throw new InvalidOperationException("Systém oznámení nepřijal naplánovanou připomínku.");
    }

    private static async Task<bool> RequestPermissionCoreAsync()
    {
        if (await LocalNotificationCenter.Current.AreNotificationsEnabled()) return true;
        return await LocalNotificationCenter.Current.RequestNotificationPermission();
    }
#endif
}
