using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Storage;
using FitnessTracker.Application.Statistics;
using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.Statistics;

public sealed class StatisticsViewModel : INotifyPropertyChanged
{
    private const string WeeklyGoalPreferenceKey = "statistics.weekly-workout-goal";
    private const int DefaultWeeklyGoal = 3;
    private readonly IWorkoutStatisticsRepository repository;
    private readonly ILogger<StatisticsViewModel> logger;
    private readonly Command reloadCommand;
    private readonly Command saveWeeklyGoalCommand;
    private WorkoutStatisticsSnapshot? snapshot;
    private bool isBusy;
    private string? errorMessage;
    private string weeklyGoalInput;
    private int weeklyGoal;
    private int weeklyFinishedCount;
    private string? goalMessage;

    public StatisticsViewModel(IWorkoutStatisticsRepository repository, ILogger<StatisticsViewModel> logger)
    {
        this.repository = repository;
        this.logger = logger;
        weeklyGoal = Math.Clamp(Preferences.Default.Get(WeeklyGoalPreferenceKey, DefaultWeeklyGoal), 1, 7);
        weeklyGoalInput = weeklyGoal.ToString(CultureInfo.CurrentCulture);
        reloadCommand = new Command(async () => await LoadAsync(), () => !IsBusy);
        saveWeeklyGoalCommand = new Command(async () => await SaveWeeklyGoalAsync(), () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand ReloadCommand => reloadCommand;
    public ICommand SaveWeeklyGoalCommand => saveWeeklyGoalCommand;
    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (!SetField(ref isBusy, value)) return;
            reloadCommand.ChangeCanExecute();
            saveWeeklyGoalCommand.ChangeCanExecute();
            OnPropertyChanged(nameof(CanInteract));
        }
    }
    public bool CanInteract => !IsBusy;
    public string WeeklyGoalInput { get => weeklyGoalInput; set => SetField(ref weeklyGoalInput, value); }
    public string WeeklyProgressText => $"{weeklyFinishedCount} / {weeklyGoal} dokončených tréninků tento týden";
    public string WeeklyGoalStatusText => weeklyFinishedCount >= weeklyGoal
        ? "Týdenní cíl je splněný."
        : $"Do cíle zbývá {weeklyGoal - weeklyFinishedCount} tréninků.";
    public string? GoalMessage { get => goalMessage; private set => SetField(ref goalMessage, value); }
    public string WorkoutCountText => snapshot?.WorkoutCount.ToString("N0", CultureInfo.CurrentCulture) ?? "—";
    public string FinishedCountText => snapshot?.FinishedWorkoutCount.ToString("N0", CultureInfo.CurrentCulture) ?? "—";
    public string SetCountText => snapshot?.SetCount.ToString("N0", CultureInfo.CurrentCulture) ?? "—";
    public string VolumeText => snapshot is null
        ? "—"
        : $"{snapshot.VolumeKilogramRepetitions.ToString("0.###", CultureInfo.CurrentCulture)} kg × opakování";
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string? ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (!SetField(ref errorMessage, value)) return;
            OnPropertyChanged(nameof(HasError));
        }
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            snapshot = await repository.GetAllTimeAsync();
            (DateTimeOffset weekStartUtc, DateTimeOffset nextWeekStartUtc) = GetCurrentLocalWeekUtc();
            weeklyFinishedCount = await repository.GetFinishedCountAsync(weekStartUtc, nextWeekStartUtc);
            NotifySummaryChanged();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se načíst statistiky tréninků.");
            ErrorMessage = "Statistiky se nepodařilo načíst. Zkus to znovu.";
        }
        finally { IsBusy = false; }
    }

    private async Task SaveWeeklyGoalAsync()
    {
        if (IsBusy) return;
        if (!int.TryParse(WeeklyGoalInput, NumberStyles.None, CultureInfo.CurrentCulture, out int target)
            || target is < 1 or > 7)
        {
            GoalMessage = "Zadej počet dokončených tréninků od 1 do 7 za týden.";
            return;
        }

        IsBusy = true;
        try
        {
            Preferences.Default.Set(WeeklyGoalPreferenceKey, target);
            weeklyGoal = target;
            GoalMessage = "Týdenní cíl uložen.";
            OnPropertyChanged(nameof(WeeklyProgressText));
            OnPropertyChanged(nameof(WeeklyGoalStatusText));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se uložit týdenní cíl tréninků.");
            GoalMessage = "Týdenní cíl se nepodařilo uložit.";
        }
        finally { IsBusy = false; }
    }

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) GetCurrentLocalWeekUtc()
    {
        DateTime localToday = DateTime.Today;
        int daysSinceMonday = ((int)localToday.DayOfWeek + 6) % 7;
        DateTime localMonday = localToday.AddDays(-daysSinceMonday);
        DateTime nextLocalMonday = localMonday.AddDays(7);

        // Hranice týdne bereme v místním čase; převod zvládne i změnu letního času.
        return (new DateTimeOffset(localMonday).ToUniversalTime(),
            new DateTimeOffset(nextLocalMonday).ToUniversalTime());
    }

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(WorkoutCountText));
        OnPropertyChanged(nameof(FinishedCountText));
        OnPropertyChanged(nameof(SetCountText));
        OnPropertyChanged(nameof(VolumeText));
        OnPropertyChanged(nameof(WeeklyProgressText));
        OnPropertyChanged(nameof(WeeklyGoalStatusText));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
