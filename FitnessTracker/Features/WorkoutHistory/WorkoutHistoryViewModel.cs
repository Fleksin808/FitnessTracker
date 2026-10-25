using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FitnessTracker.Application.Workouts;
using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.WorkoutHistory;

/// <summary>Načítá historii a filtruje ji podle místního data začátku.</summary>
public sealed class WorkoutHistoryViewModel : INotifyPropertyChanged
{
    private readonly IWorkoutRepository repository;
    private readonly ILogger<WorkoutHistoryViewModel> logger;
    private readonly List<WorkoutHistoryItem> allItems = [];
    private readonly Command reloadCommand;
    private readonly Command clearFilterCommand;
    private DateTime fromDate = DateTime.Today;
    private DateTime toDate = DateTime.Today;
    private bool useFromDate;
    private bool useToDate;
    private bool isLoading;
    private string? errorMessage;
    private string filterMessage = "Zobrazeny jsou všechny tréninky.";

    public WorkoutHistoryViewModel(IWorkoutRepository repository, ILogger<WorkoutHistoryViewModel> logger)
    {
        this.repository = repository;
        this.logger = logger;
        reloadCommand = new Command(async () => await LoadAsync(), () => !IsLoading);
        clearFilterCommand = new Command(ClearFilter);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<WorkoutHistoryItem> Workouts { get; } = [];
    public ICommand ReloadCommand => reloadCommand;
    public ICommand ClearFilterCommand => clearFilterCommand;

    public DateTime FromDate
    {
        get => fromDate;
        set { if (SetField(ref fromDate, value.Date)) ApplyFilter(); }
    }

    public DateTime ToDate
    {
        get => toDate;
        set { if (SetField(ref toDate, value.Date)) ApplyFilter(); }
    }

    public bool UseFromDate
    {
        get => useFromDate;
        set { if (SetField(ref useFromDate, value)) ApplyFilter(); }
    }

    public bool UseToDate
    {
        get => useToDate;
        set { if (SetField(ref useToDate, value)) ApplyFilter(); }
    }

    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            if (!SetField(ref isLoading, value)) return;
            reloadCommand.ChangeCanExecute();
        }
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetField(ref errorMessage, value);
    }

    public string FilterMessage
    {
        get => filterMessage;
        private set => SetField(ref filterMessage, value);
    }

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var loaded = await repository.GetAllAsync();
            allItems.Clear();
            allItems.AddRange(loaded.Select(workout => new WorkoutHistoryItem(workout)));
            ApplyFilter();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se načíst historii tréninků.");
            ErrorMessage = "Historii se nepodařilo načíst. Zkus to znovu.";
        }
        finally { IsLoading = false; }
    }

    private void ApplyFilter()
    {
        if (UseFromDate && UseToDate && FromDate.Date > ToDate.Date)
        {
            FilterMessage = "Počáteční datum nesmí být po koncovém datu.";
            Workouts.Clear();
            return;
        }

        var filtered = allItems.Where(item =>
        {
            // Filtrujeme podle místního kalendářního dne, který uživatel vidí v seznamu.
            DateTime localDate = item.Workout.StartedAt.ToLocalTime().Date;
            return (!UseFromDate || localDate >= FromDate.Date)
                && (!UseToDate || localDate <= ToDate.Date);
        });

        Workouts.Clear();
        foreach (var item in filtered) Workouts.Add(item);
        FilterMessage = Workouts.Count == allItems.Count
            ? $"Zobrazeno {Workouts.Count} tréninků."
            : $"Zobrazeno {Workouts.Count} z {allItems.Count} tréninků.";
    }

    private void ClearFilter()
    {
        // Vypnutí přepínačů zachová datum v ovládacích prvcích, ale zobrazí celou historii.
        UseFromDate = false;
        UseToDate = false;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
