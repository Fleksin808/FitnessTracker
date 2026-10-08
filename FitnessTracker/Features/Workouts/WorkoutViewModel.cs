using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FitnessTracker.Application.Workouts;
using FitnessTracker.Domain.Workouts;
using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.Workouts;

// Rozhraní umožňuje bindingu reagovat na změny vlastností po vytvoření stránky.
public sealed class WorkoutViewModel : INotifyPropertyChanged
{
    private readonly IWorkoutRepository repository;
    private readonly ILogger<WorkoutViewModel> logger;
    private readonly Command confirmWorkoutCommand;
    private readonly Command loadWorkoutsCommand;
    private string? workoutName = string.Empty;
    private string workoutResult = "Zatím nemáš zadaný trénink.";
    private bool isSaving;
    private bool isLoading;
    private string? loadError;

    public WorkoutViewModel(IWorkoutRepository repository, ILogger<WorkoutViewModel> logger)
    {
        // DI předá úložiště a logger; ViewModel nezná konkrétní způsob ukládání.
        this.repository = repository;
        this.logger = logger;

        // Command očekává akci bez Task; asynchronní lambda zavolá metodu s ošetřením chyb.
        confirmWorkoutCommand = new Command(
            async () => await ConfirmWorkoutAsync(),
            () => !IsSaving && !IsLoading);
        loadWorkoutsCommand = new Command(
            async () => await LoadWorkoutsAsync(),
            () => !IsSaving && !IsLoading);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // Na rozdíl od List kolekce oznamuje přidání a odebrání položek budoucímu seznamu v UI.
    // Instanci zachováváme; při načtení měníme pouze její obsah.
    public ObservableCollection<Workout> Workouts { get; } = new();

    public ICommand LoadWorkoutsCommand => loadWorkoutsCommand;

    public string? LoadError
    {
        get => loadError;
        private set
        {
            if (loadError == value)
                return;

            loadError = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            if (isLoading == value)
                return;

            isLoading = value;
            OnPropertyChanged();
            UpdateCommands();
        }
    }

    public string? WorkoutName
    {
        get => workoutName;
        set
        {
            // Stejná hodnota nevyžaduje další oznámení ani aktualizaci zobrazení.
            if (workoutName == value)
                return;

            workoutName = value;
            OnPropertyChanged();
        }
    }

    public string WorkoutResult
    {
        get => workoutResult;
        private set
        {
            if (workoutResult == value)
                return;

            workoutResult = value;
            // Bez oznámení by Label stále zobrazoval původní text.
            OnPropertyChanged();
        }
    }

    public ICommand ConfirmWorkoutCommand => confirmWorkoutCommand;

    public bool IsSaving
    {
        get => isSaving;
        private set
        {
            if (isSaving == value)
                return;

            isSaving = value;
            OnPropertyChanged();
            // Tlačítko znovu vyhodnotí CanExecute a během ukládání se zakáže.
            UpdateCommands();
        }
    }

    public async Task ConfirmWorkoutAsync()
    {
        // Chráníme i přímé zavolání metody; nestačí pouze zakázat tlačítko.
        if (IsSaving || IsLoading)
            return;

        // Odstraníme okrajové mezery; null může přijít z prázdného vstupního pole.
        string? name = WorkoutName?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            WorkoutResult = "Nejdřív zadej název tréninku.";
            // Neplatný vstup ukončí operaci před zobrazením úspěšného výsledku.
            return;
        }

        IsSaving = true;

        try
        {
            WorkoutResult = "Ukládám trénink…";
            // Zatím předpokládáme, že uživatel začíná cvičit právě teď.
            var workout = new Workout(name, DateTimeOffset.Now);
            await repository.AddAsync(workout);

            // Obnova seznamu má vlastní ošetření chyby: selhání čtení neznamená selhání zápisu.
            bool refreshed = await RefreshWorkoutsAsync();
            WorkoutResult = refreshed
                ? $"Trénink přidán do paměti: {workout.Name}"
                : "Trénink byl přidán, ale seznam se nepodařilo obnovit. Zkus načíst seznam znovu.";
        }
        catch (Exception exception)
        {
            // Chyba nesmí uniknout z asynchronního příkazu; podrobnosti patří do logu.
            logger.LogError(exception, "Nepodařilo se přidat trénink.");
            WorkoutResult = "Trénink se nepodařilo přidat. Zkus to znovu.";
        }
        finally
        {
            // Stav obnovíme při úspěchu i chybě, aby šlo formulář použít znovu.
            IsSaving = false;
        }
    }

    public async Task LoadWorkoutsAsync()
    {
        // Souběžná načítání ani čtení během zápisu nespouštíme.
        if (IsLoading || IsSaving)
            return;

        await RefreshWorkoutsAsync();
    }

    private async Task<bool> RefreshWorkoutsAsync()
    {
        IsLoading = true;
        LoadError = null;

        try
        {
            var loadedWorkouts = await repository.GetAllAsync();

            // Staré položky odstraníme až po úspěšném načtení, aby chyba nesmazala zobrazená data.
            // Metodu voláme z UI vlákna a await zachová jeho kontext pro změny kolekce.
            Workouts.Clear();
            foreach (var workout in loadedWorkouts)
                Workouts.Add(workout);

            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se načíst seznam tréninků.");
            LoadError = "Tréninky se nepodařilo načíst. Zkus to znovu.";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateCommands()
    {
        // Oba příkazy sdílejí podmínku: při čtení nebo zápisu nesmí začít další operace.
        confirmWorkoutCommand.ChangeCanExecute();
        loadWorkoutsCommand.ChangeCanExecute();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        // CallerMemberName doplní název vlastnosti místo ručně zapsaného řetězce.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
