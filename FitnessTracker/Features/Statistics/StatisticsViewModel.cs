using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FitnessTracker.Application.Statistics;
using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.Statistics;

public sealed class StatisticsViewModel : INotifyPropertyChanged
{
    private readonly IWorkoutStatisticsRepository repository;
    private readonly ILogger<StatisticsViewModel> logger;
    private readonly Command reloadCommand;
    private WorkoutStatisticsSnapshot? snapshot;
    private bool isBusy;
    private string? errorMessage;

    public StatisticsViewModel(IWorkoutStatisticsRepository repository, ILogger<StatisticsViewModel> logger)
    {
        this.repository = repository;
        this.logger = logger;
        reloadCommand = new Command(async () => await LoadAsync(), () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand ReloadCommand => reloadCommand;
    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (!SetField(ref isBusy, value)) return;
            reloadCommand.ChangeCanExecute();
        }
    }
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
            NotifySummaryChanged();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se načíst statistiky tréninků.");
            ErrorMessage = "Statistiky se nepodařilo načíst. Zkus to znovu.";
        }
        finally { IsBusy = false; }
    }

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(WorkoutCountText));
        OnPropertyChanged(nameof(FinishedCountText));
        OnPropertyChanged(nameof(SetCountText));
        OnPropertyChanged(nameof(VolumeText));
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
