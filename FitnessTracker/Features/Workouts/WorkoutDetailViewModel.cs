using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FitnessTracker.Application.Exercises;
using FitnessTracker.Application.Workouts;
using FitnessTracker.Domain.Exercises;
using FitnessTracker.Domain.Workouts;
using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.Workouts;

public sealed class WorkoutDetailViewModel : INotifyPropertyChanged
{
    private readonly IWorkoutLogRepository repository;
    private readonly IExerciseRepository exerciseRepository;
    private readonly ILogger<WorkoutDetailViewModel> logger;
    private readonly Command[] commands;
    private Workout? workout;
    private Guid workoutId;
    private bool isBusy;
    private bool needsReload = true;
    private string? errorMessage;
    private string? statusMessage;
    private string? repetitionsInput = "10";
    private string? loadKilogramsInput;
    private Exercise? selectedExercise;
    private WorkoutExerciseItem? selectedRecordedExercise;

    public WorkoutDetailViewModel(IWorkoutLogRepository repository, IExerciseRepository exerciseRepository,
        ILogger<WorkoutDetailViewModel> logger)
    {
        this.repository = repository;
        this.exerciseRepository = exerciseRepository;
        this.logger = logger;
        var retry = new Command(async () => await LoadAsync(workoutId), () => !IsBusy && workoutId != Guid.Empty);
        var addExercise = new Command(async () => await AddExerciseAsync(), () => CanRecord);
        var addSet = new Command(async () => await AddSetAsync(), () => CanRecord);
        var finish = new Command(async () => await FinishAsync(), () => CanRecord);
        var exerciseUp = new Command<Guid>(async id => await MoveExerciseAsync(id, -1), _ => CanRecord);
        var exerciseDown = new Command<Guid>(async id => await MoveExerciseAsync(id, 1), _ => CanRecord);
        var setUp = new Command<Guid>(async id => await MoveSetAsync(id, -1), _ => CanRecord);
        var setDown = new Command<Guid>(async id => await MoveSetAsync(id, 1), _ => CanRecord);
        commands = [retry, addExercise, addSet, finish, exerciseUp, exerciseDown, setUp, setDown];
        RetryCommand = retry;
        AddExerciseCommand = addExercise;
        AddSetCommand = addSet;
        FinishCommand = finish;
        MoveExerciseUpCommand = exerciseUp;
        MoveExerciseDownCommand = exerciseDown;
        MoveSetUpCommand = setUp;
        MoveSetDownCommand = setDown;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand RetryCommand { get; }
    public ICommand AddExerciseCommand { get; }
    public ICommand AddSetCommand { get; }
    public ICommand FinishCommand { get; }
    public ICommand MoveExerciseUpCommand { get; }
    public ICommand MoveExerciseDownCommand { get; }
    public ICommand MoveSetUpCommand { get; }
    public ICommand MoveSetDownCommand { get; }
    public ObservableCollection<Exercise> AvailableExercises { get; } = new();
    public ObservableCollection<WorkoutExerciseItem> RecordedExercises { get; } = new();
    public Exercise? SelectedExercise { get => selectedExercise; set => SetField(ref selectedExercise, value); }
    public WorkoutExerciseItem? SelectedRecordedExercise { get => selectedRecordedExercise; set => SetField(ref selectedRecordedExercise, value); }
    public string? RepetitionsInput { get => repetitionsInput; set => SetField(ref repetitionsInput, value); }
    public string? LoadKilogramsInput { get => loadKilogramsInput; set => SetField(ref loadKilogramsInput, value); }
    public string? StatusMessage { get => statusMessage; private set => SetField(ref statusMessage, value); }

    public Workout? Workout
    {
        get => workout;
        private set
        {
            if (!SetField(ref workout, value)) return;
            OnPropertyChanged(nameof(HasWorkout));
            OnPropertyChanged(nameof(WorkoutName));
            OnPropertyChanged(nameof(StartedAtText));
            OnPropertyChanged(nameof(FinishedAtText));
            OnPropertyChanged(nameof(IsOpenWorkout));
            UpdateCommands();
        }
    }
    public bool HasWorkout => Workout is not null;
    public string WorkoutName => Workout?.Name ?? string.Empty;
    public string StartedAtText => Workout?.StartedAt.ToLocalTime().ToString("g") ?? string.Empty;
    public string FinishedAtText => Workout?.FinishedAt is { } end ? $"Ukončeno: {end.ToLocalTime():g}" : "Trénink probíhá";
    public bool IsOpenWorkout => Workout is { IsFinished: false };
    public bool CanRecord => IsOpenWorkout && !IsBusy && !needsReload;
    public bool IsLoading => IsBusy;
    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (!SetField(ref isBusy, value)) return;
            OnPropertyChanged(nameof(IsLoading));
            UpdateCommands();
        }
    }
    public string? ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (SetField(ref errorMessage, value)) OnPropertyChanged(nameof(HasError));
        }
    }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public async Task LoadAsync(Guid id)
    {
        if (IsBusy) return;
        workoutId = id;
        IsBusy = true;
        Workout = null;
        RecordedExercises.Clear();
        AvailableExercises.Clear();
        SelectedExercise = null;
        SelectedRecordedExercise = null;
        StatusMessage = null;
        needsReload = true;
        try
        {
            if (id == Guid.Empty)
            {
                ErrorMessage = "Chybí platné ID tréninku. Vrať se do seznamu.";
                return;
            }
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }

    public async Task AddExerciseAsync()
    {
        if (!CanRecord) return;
        if (SelectedExercise is not { } exercise)
        {
            StatusMessage = "Vyber cvik z aktivního katalogu. Nové cviky přidáš v sekci Cviky.";
            return;
        }
        await WriteAsync(() => repository.AddExerciseAsync(workoutId, exercise.Id), "Cvik přidán do tréninku.");
    }

    public async Task AddSetAsync()
    {
        if (!CanRecord) return;
        if (SelectedRecordedExercise is not { } selected)
        {
            StatusMessage = "Vyber zaznamenaný cvik, ke kterému série patří.";
            return;
        }
        if (!int.TryParse(RepetitionsInput, NumberStyles.Integer, CultureInfo.CurrentCulture, out int repetitions) || repetitions < 1)
        {
            StatusMessage = "Počet opakování musí být kladné celé číslo.";
            return;
        }
        long? grams = null;
        if (!string.IsNullOrWhiteSpace(LoadKilogramsInput))
        {
            // Nepovolujeme oddělovače tisíců: chybný desetinný oddělovač nesmí změnit 2,5 na 25.
            const NumberStyles style = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;
            if (!decimal.TryParse(LoadKilogramsInput.Trim(), style, CultureInfo.CurrentCulture, out decimal kg)
                || kg < 0 || kg > (decimal)long.MaxValue / 1000m || decimal.Truncate(kg * 1000m) != kg * 1000m)
            {
                StatusMessage = "Zadej nezápornou zátěž v kg s přesností nejvýše na gramy a desetinným oddělovačem zařízení.";
                return;
            }
            grams = (long)(kg * 1000m);
        }
        await WriteAsync(() => repository.AddSetAsync(selected.Exercise.Id, repetitions, grams), "Série uložena.");
    }

    public Task MoveExerciseAsync(Guid id, int direction) => WriteAsync(() => repository.MoveExerciseAsync(id, direction), "Pořadí cviků uloženo.");
    public Task MoveSetAsync(Guid id, int direction) => WriteAsync(() => repository.MoveSetAsync(id, direction), "Pořadí sérií uloženo.");
    public Task FinishAsync() => WriteAsync(() => repository.FinishAsync(workoutId, DateTimeOffset.Now), "Trénink ukončen.");

    private async Task WriteAsync(Func<Task> operation, string successMessage)
    {
        if (!CanRecord) return;
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            await operation();
            // Po úspěšném zápisu vyžadujeme čerstvý snímek. Chyba čtení nesmí vést k dvojímu zápisu.
            needsReload = true;
            bool refreshed = await RefreshAsync();
            StatusMessage = refreshed ? successMessage : successMessage + " Detail znovu načti před dalším zápisem.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se změnit záznam tréninku {WorkoutId}.", workoutId);
            ErrorMessage = exception is WorkoutRecordingException ? exception.Message
                : "Změnu se nepodařilo uložit. Znovu načti detail a zkus to znovu.";
        }
        finally { IsBusy = false; }
    }

    private async Task<bool> RefreshAsync()
    {
        ErrorMessage = null;
        try
        {
            var log = await repository.GetAsync(workoutId);
            if (log is null)
            {
                Workout = null;
                ErrorMessage = "Tento trénink už není dostupný. Vrať se do seznamu.";
                return false;
            }
            // Ukončená historie nevyžaduje načítání aktivní nabídky cviků.
            var available = log.Workout.IsFinished ? Array.Empty<Exercise>() : await exerciseRepository.GetAllAsync();
            Guid? selectedId = SelectedRecordedExercise?.Exercise.Id;
            Guid? catalogId = SelectedExercise?.Id;
            Workout = log.Workout;
            RecordedExercises.Clear();
            foreach (var item in log.Exercises) RecordedExercises.Add(new WorkoutExerciseItem(item));
            AvailableExercises.Clear();
            foreach (var item in available) AvailableExercises.Add(item);
            SelectedRecordedExercise = RecordedExercises.FirstOrDefault(x => x.Exercise.Id == selectedId) ?? RecordedExercises.FirstOrDefault();
            SelectedExercise = AvailableExercises.FirstOrDefault(x => x.Id == catalogId);
            needsReload = false;
            UpdateCommands();
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se načíst záznam tréninku {WorkoutId}.", workoutId);
            ErrorMessage = "Detail se nepodařilo načíst. Zkus to znovu.";
            return false;
        }
    }

    private void UpdateCommands()
    {
        OnPropertyChanged(nameof(CanRecord));
        foreach (var command in commands) command.ChangeCanExecute();
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
