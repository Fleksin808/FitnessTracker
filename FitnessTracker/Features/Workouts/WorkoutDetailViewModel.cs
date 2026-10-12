using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FitnessTracker.Application.Workouts;
using FitnessTracker.Domain.Workouts;
using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.Workouts;

public sealed class WorkoutDetailViewModel : INotifyPropertyChanged
{
    private readonly IWorkoutRepository repository;
    private readonly ILogger<WorkoutDetailViewModel> logger;
    private readonly Command retryCommand;
    private Workout? workout;
    private Guid workoutId;
    private bool isLoading;
    private string? errorMessage;

    public WorkoutDetailViewModel(IWorkoutRepository repository, ILogger<WorkoutDetailViewModel> logger)
    {
        // DI předá stejné úložiště jako seznamu; detail si nevytváří vlastní prázdnou instanci.
        this.repository = repository;
        this.logger = logger;
        retryCommand = new Command(
            async () => await LoadAsync(workoutId),
            () => !IsLoading && workoutId != Guid.Empty);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand RetryCommand => retryCommand;

    public Workout? Workout
    {
        get => workout;
        private set
        {
            if (workout == value)
                return;

            workout = value;
            OnPropertyChanged();
            // Tyto vlastnosti se počítají z Workout; změnu musíme oznámit i pro ně.
            OnPropertyChanged(nameof(HasWorkout));
            OnPropertyChanged(nameof(WorkoutName));
            OnPropertyChanged(nameof(StartedAtText));
        }
    }

    public bool HasWorkout => Workout is not null;
    public string WorkoutName => Workout?.Name ?? string.Empty;

    // Uložení zůstává v UTC; pro uživatele zobrazíme místní datum a čas podle jeho kultury.
    public string StartedAtText => Workout?.StartedAt.ToLocalTime().ToString("g") ?? string.Empty;

    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            if (isLoading == value)
                return;

            isLoading = value;
            OnPropertyChanged();
            retryCommand.ChangeCanExecute();
        }
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (errorMessage == value)
                return;

            errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public async Task LoadAsync(Guid id)
    {
        // Opakované kliknutí na načtení nesmí spustit druhou souběžnou operaci.
        if (IsLoading)
            return;

        workoutId = id;
        IsLoading = true;
        ErrorMessage = null;
        Workout = null;

        try
        {
            if (id == Guid.Empty)
            {
                ErrorMessage = "Chybí platné ID tréninku. Vrať se do seznamu a vyber trénink.";
                return;
            }

            // Předáváme ID, nikoliv kopii tréninku; zdrojem dat je stále repository.
            Workout = await repository.GetByIdAsync(id);
            if (Workout is null)
                ErrorMessage = "Tento trénink už není dostupný. Vrať se do seznamu.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se načíst detail tréninku {WorkoutId}.", id);
            ErrorMessage = "Detail se nepodařilo načíst. Zkus to znovu.";
        }
        finally
        {
            // Stav obnovíme i při chybě nebo neplatném ID.
            IsLoading = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
