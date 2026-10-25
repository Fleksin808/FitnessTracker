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
    private readonly IWorkoutRepository workoutRepository;
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
    private WorkoutSetItem? selectedSetForEdit;
    private string? editRepetitionsInput;
    private string? editLoadKilogramsInput;

    public WorkoutDetailViewModel(IWorkoutLogRepository repository, IWorkoutRepository workoutRepository,
        IExerciseRepository exerciseRepository,
        ILogger<WorkoutDetailViewModel> logger)
    {
        this.repository = repository;
        this.workoutRepository = workoutRepository;
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
        var editSet = new Command<Guid>(BeginEditSet, _ => CanEditRecordedData);
        var saveSetEdit = new Command(async () => await SaveSetEditAsync(), () => CanEditRecordedData && IsEditingSet);
        var cancelSetEdit = new Command(CancelSetEdit, () => !IsBusy && IsEditingSet);
        commands = [retry, addExercise, addSet, finish, exerciseUp, exerciseDown, setUp, setDown,
            editSet, saveSetEdit, cancelSetEdit];
        RetryCommand = retry;
        AddExerciseCommand = addExercise;
        AddSetCommand = addSet;
        FinishCommand = finish;
        MoveExerciseUpCommand = exerciseUp;
        MoveExerciseDownCommand = exerciseDown;
        MoveSetUpCommand = setUp;
        MoveSetDownCommand = setDown;
        EditSetCommand = editSet;
        SaveSetEditCommand = saveSetEdit;
        CancelSetEditCommand = cancelSetEdit;
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
    public ICommand EditSetCommand { get; }
    public ICommand SaveSetEditCommand { get; }
    public ICommand CancelSetEditCommand { get; }
    public ObservableCollection<Exercise> AvailableExercises { get; } = new();
    public ObservableCollection<WorkoutExerciseItem> RecordedExercises { get; } = new();
    public Exercise? SelectedExercise { get => selectedExercise; set => SetField(ref selectedExercise, value); }
    public WorkoutExerciseItem? SelectedRecordedExercise { get => selectedRecordedExercise; set => SetField(ref selectedRecordedExercise, value); }
    public string? RepetitionsInput { get => repetitionsInput; set => SetField(ref repetitionsInput, value); }
    public string? LoadKilogramsInput { get => loadKilogramsInput; set => SetField(ref loadKilogramsInput, value); }
    public string? EditRepetitionsInput { get => editRepetitionsInput; set => SetField(ref editRepetitionsInput, value); }
    public string? EditLoadKilogramsInput { get => editLoadKilogramsInput; set => SetField(ref editLoadKilogramsInput, value); }
    public bool IsEditingSet => SelectedSetForEdit is not null;
    public string SelectedSetText => SelectedSetForEdit?.DisplayText ?? string.Empty;
    public bool CanEditRecordedData => Workout is not null && !IsBusy && !needsReload;
    public bool CanDeleteWorkout => Workout is not null && !IsBusy;
    public string? StatusMessage { get => statusMessage; private set => SetField(ref statusMessage, value); }

    private WorkoutSetItem? SelectedSetForEdit
    {
        get => selectedSetForEdit;
        set
        {
            if (!SetField(ref selectedSetForEdit, value)) return;
            OnPropertyChanged(nameof(IsEditingSet));
            OnPropertyChanged(nameof(SelectedSetText));
            UpdateCommands();
        }
    }

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
            OnPropertyChanged(nameof(CanEditRecordedData));
            OnPropertyChanged(nameof(CanDeleteWorkout));
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
        SelectedSetForEdit = null;
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
        if (!TryParseSetInputs(RepetitionsInput, LoadKilogramsInput, out int repetitions, out long? grams)) return;
        await WriteAsync(() => repository.AddSetAsync(selected.Exercise.Id, repetitions, grams), "Série uložena.");
    }

    private void BeginEditSet(Guid setId)
    {
        if (!CanEditRecordedData) return;
        var item = RecordedExercises.SelectMany(exercise => exercise.Sets)
            .FirstOrDefault(set => set.Set.Id == setId);
        if (item is null)
        {
            StatusMessage = "Tato série už není dostupná. Znovu načti detail.";
            return;
        }

        SelectedSetForEdit = item;
        EditRepetitionsInput = item.Set.Repetitions.ToString(CultureInfo.CurrentCulture);
        EditLoadKilogramsInput = item.Set.LoadGrams is { } grams
            ? (grams / 1000m).ToString("0.###", CultureInfo.CurrentCulture)
            : string.Empty;
        StatusMessage = "Uprav opakování nebo zátěž a změnu ulož.";
    }

    private async Task SaveSetEditAsync()
    {
        if (!CanEditRecordedData || SelectedSetForEdit is not { } selected) return;
        if (!TryParseSetInputs(EditRepetitionsInput, EditLoadKilogramsInput,
                out int repetitions, out long? grams)) return;

        bool saved = await WriteAsync(() => repository.UpdateSetAsync(selected.Set.Id, repetitions, grams),
            "Oprava série uložena.", allowFinishedWorkout: true);
        if (saved) SelectedSetForEdit = null;
    }

    private void CancelSetEdit()
    {
        SelectedSetForEdit = null;
        StatusMessage = null;
    }

    private bool TryParseSetInputs(string? repetitionsText, string? kilogramsText,
        out int repetitions, out long? grams)
    {
        grams = null;
        if (!int.TryParse(repetitionsText, NumberStyles.Integer, CultureInfo.CurrentCulture, out repetitions)
            || repetitions < 1)
        {
            StatusMessage = "Počet opakování musí být kladné celé číslo.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(kilogramsText)) return true;

        // Stejné přísné parsování chrání novou sérii i opravu před chybným desetinným oddělovačem.
        const NumberStyles style = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;
        if (!decimal.TryParse(kilogramsText.Trim(), style, CultureInfo.CurrentCulture, out decimal kg)
            || kg < 0 || kg > (decimal)long.MaxValue / 1000m || decimal.Truncate(kg * 1000m) != kg * 1000m)
        {
            StatusMessage = "Zadej nezápornou zátěž v kg s přesností nejvýše na gramy a desetinným oddělovačem zařízení.";
            return false;
        }
        grams = (long)(kg * 1000m);
        return true;
    }

    public async Task MoveExerciseAsync(Guid id, int direction) =>
        await WriteAsync(() => repository.MoveExerciseAsync(id, direction), "Pořadí cviků uloženo.");
    public async Task MoveSetAsync(Guid id, int direction) =>
        await WriteAsync(() => repository.MoveSetAsync(id, direction), "Pořadí sérií uloženo.");
    public async Task FinishAsync() =>
        await WriteAsync(() => repository.FinishAsync(workoutId, DateTimeOffset.Now), "Trénink ukončen.");

    public async Task<bool> DeleteWorkoutAsync()
    {
        if (!CanDeleteWorkout) return false;
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            await workoutRepository.DeleteAsync(workoutId);
            // Po úspěchu vyčistíme detail; stránka se vrátí do seznamu nebo historie.
            Workout = null;
            RecordedExercises.Clear();
            AvailableExercises.Clear();
            SelectedRecordedExercise = null;
            SelectedExercise = null;
            SelectedSetForEdit = null;
            needsReload = true;
            StatusMessage = "Trénink i jeho zaznamenané série byly smazány.";
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se smazat trénink {WorkoutId}.", workoutId);
            ErrorMessage = "Trénink se nepodařilo smazat. Zkus to znovu.";
            return false;
        }
        finally { IsBusy = false; }
    }

    private async Task<bool> WriteAsync(Func<Task> operation, string successMessage, bool allowFinishedWorkout = false)
    {
        // Oprava uložené série je jediný zápis dovolený i u ukončeného tréninku.
        if (allowFinishedWorkout ? !CanEditRecordedData : !CanRecord) return false;
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
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se změnit záznam tréninku {WorkoutId}.", workoutId);
            ErrorMessage = exception is WorkoutRecordingException ? exception.Message
                : "Změnu se nepodařilo uložit. Znovu načti detail a zkus to znovu.";
            return false;
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
            Guid? editingSetId = SelectedSetForEdit?.Set.Id;
            Workout = log.Workout;
            RecordedExercises.Clear();
            foreach (var item in log.Exercises) RecordedExercises.Add(new WorkoutExerciseItem(item));
            AvailableExercises.Clear();
            foreach (var item in available) AvailableExercises.Add(item);
            SelectedRecordedExercise = RecordedExercises.FirstOrDefault(x => x.Exercise.Id == selectedId) ?? RecordedExercises.FirstOrDefault();
            SelectedSetForEdit = editingSetId is { } id
                ? RecordedExercises.SelectMany(exercise => exercise.Sets).FirstOrDefault(set => set.Set.Id == id)
                : null;
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
        OnPropertyChanged(nameof(CanEditRecordedData));
        OnPropertyChanged(nameof(CanDeleteWorkout));
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
