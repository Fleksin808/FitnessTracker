using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FitnessTracker.Application.Exercises;
using FitnessTracker.Application.WorkoutTemplates;
using FitnessTracker.Domain.Exercises;
using FitnessTracker.Domain.WorkoutTemplates;
using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.WorkoutTemplates;

public sealed class WorkoutTemplateItem(WorkoutTemplate template, IReadOnlyDictionary<Guid, Exercise> catalog)
{
    public WorkoutTemplate Template { get; } = template;
    public Guid Id => Template.Id;
    public string Name => Template.Name;
    public string ExerciseNames => string.Join(" → ", Template.Exercises.Select(item =>
        catalog.TryGetValue(item.ExerciseId, out var exercise)
            ? exercise.Name + (exercise.IsArchived ? " (archivováno)" : "") : "(chybějící cvik)"));
}

public sealed class WorkoutTemplateExerciseItem(Exercise exercise)
{
    public Exercise Exercise { get; } = exercise;
    public string Name => Exercise.Name;
}

public sealed class WorkoutTemplatesViewModel : INotifyPropertyChanged
{
    private readonly IWorkoutTemplateRepository repository;
    private readonly IExerciseRepository exerciseRepository;
    private readonly ILogger<WorkoutTemplatesViewModel> logger;
    private readonly Command[] commands;
    private IReadOnlyDictionary<Guid, Exercise> exerciseCatalog = new Dictionary<Guid, Exercise>();
    private Guid? editingId;
    private string? name;
    private Exercise? selectedExercise;
    private string? statusMessage;
    private string? loadError;
    private bool isBusy;
    private bool isEditorOpen;

    public WorkoutTemplatesViewModel(IWorkoutTemplateRepository repository, IExerciseRepository exerciseRepository,
        ILogger<WorkoutTemplatesViewModel> logger)
    {
        this.repository = repository;
        this.exerciseRepository = exerciseRepository;
        this.logger = logger;
        SaveCommand = new Command(async () => await SaveAsync(), () => !IsBusy);
        AddExerciseCommand = new Command(AddExercise, () => !IsBusy && SelectedExercise is not null);
        RemoveExerciseCommand = new Command<WorkoutTemplateExerciseItem>(RemoveExercise, _ => !IsBusy);
        NewCommand = new Command(StartNew, () => !IsBusy);
        EditCommand = new Command<WorkoutTemplateItem>(BeginEdit, _ => !IsBusy);
        DeleteCommand = new Command<WorkoutTemplateItem>(async item => await DeleteAsync(item), _ => !IsBusy);
        UseCommand = new Command<WorkoutTemplateItem>(async item => await CreateWorkoutAsync(item), _ => !IsBusy);
        CancelCommand = new Command(CancelEdit, () => !IsBusy);
        RefreshCommand = new Command(async () => await LoadAsync(), () => !IsBusy);
        commands = [ (Command)SaveCommand, (Command)AddExerciseCommand, (Command)RemoveExerciseCommand,
            (Command)NewCommand, (Command)EditCommand, (Command)DeleteCommand, (Command)UseCommand,
            (Command)CancelCommand, (Command)RefreshCommand ];
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<WorkoutTemplateItem> Templates { get; } = [];
    public ObservableCollection<Exercise> AvailableExercises { get; } = [];
    public ObservableCollection<WorkoutTemplateExerciseItem> PlannedExercises { get; } = [];
    public ICommand SaveCommand { get; }
    public ICommand AddExerciseCommand { get; }
    public ICommand RemoveExerciseCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand UseCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RefreshCommand { get; }
    public string? Name { get => name; set => SetField(ref name, value); }
    public Exercise? SelectedExercise { get => selectedExercise; set { if (SetField(ref selectedExercise, value)) UpdateCommands(); } }
    public string? StatusMessage { get => statusMessage; private set => SetField(ref statusMessage, value); }
    public string? LoadError { get => loadError; private set => SetField(ref loadError, value); }
    public bool IsEditing => isEditorOpen;
    public bool IsBusy { get => isBusy; private set { if (SetField(ref isBusy, value)) UpdateCommands(); } }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var allExercises = await exerciseRepository.GetAllAsync(includeArchived: true);
            var catalog = allExercises.ToDictionary(exercise => exercise.Id);
            exerciseCatalog = catalog;
            var templates = await repository.GetAllAsync();
            Templates.Clear();
            foreach (var template in templates) Templates.Add(new(template, catalog));
            AvailableExercises.Clear();
            foreach (var exercise in allExercises.Where(exercise => !exercise.IsArchived)) AvailableExercises.Add(exercise);
            LoadError = null;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se načíst šablony tréninků.");
            LoadError = "Šablony se nepodařilo načíst. Zkus obnovit seznam.";
        }
        finally { IsBusy = false; }
    }

    public async Task<Guid?> CreateWorkoutAsync(WorkoutTemplateItem? item)
    {
        if (IsBusy || item is null) return null;
        IsBusy = true;
        try
        {
            // Čas začátku vznikne až při použití šablony a uloží se v UTC.
            Guid id = await repository.CreateWorkoutAsync(item.Id, DateTimeOffset.UtcNow);
            StatusMessage = $"Trénink „{item.Name}“ byl vytvořen.";
            return id;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se vytvořit trénink ze šablony.");
            StatusMessage = exception is WorkoutTemplateException
                ? exception.Message : "Trénink se nepodařilo vytvořit. Zkus to znovu.";
            return null;
        }
        finally { IsBusy = false; }
    }

    public void StartNew()
    {
        if (IsBusy) return;
        editingId = null;
        Name = string.Empty;
        PlannedExercises.Clear();
        isEditorOpen = true;
        NotifyEditor();
        StatusMessage = null;
    }
    public void CancelEdit() { if (!IsBusy) SetEditor(null); }

    private void AddExercise()
    {
        if (IsBusy || SelectedExercise is null) return;
        if (PlannedExercises.Any(item => item.Exercise.Id == SelectedExercise.Id))
        {
            StatusMessage = "Tento cvik už v šabloně je.";
            return;
        }
        PlannedExercises.Add(new(SelectedExercise));
        SelectedExercise = null;
        StatusMessage = null;
    }

    private void RemoveExercise(WorkoutTemplateExerciseItem? item)
    {
        if (!IsBusy || item is null) return;
        PlannedExercises.Remove(item);
    }

    private void BeginEdit(WorkoutTemplateItem? item)
    {
        if (IsBusy || item is null) return;
        editingId = item.Id;
        isEditorOpen = true;
        Name = item.Name;
        PlannedExercises.Clear();
        foreach (var planned in item.Template.Exercises)
            if (exerciseCatalog.TryGetValue(planned.ExerciseId, out var exercise)) PlannedExercises.Add(new(exercise));
        NotifyEditor();
        StatusMessage = null;
    }

    private async Task SaveAsync()
    {
        if (IsBusy) return;
        string? cleanedName = Name?.Trim();
        if (string.IsNullOrWhiteSpace(cleanedName)) { StatusMessage = "Zadej název šablony."; return; }
        if (PlannedExercises.Count == 0) { StatusMessage = "Přidej do šablony alespoň jeden cvik."; return; }
        IsBusy = true;
        try
        {
            var ids = PlannedExercises.Select(item => item.Exercise.Id).ToArray();
            if (ids.Distinct().Count() != ids.Length) { StatusMessage = "Stejný cvik nelze do šablony přidat dvakrát."; return; }
            if (editingId is Guid id)
                await repository.UpdateAsync(Templates.First(item => item.Id == id).Template.WithContents(cleanedName, ids));
            else
                await repository.AddAsync(new WorkoutTemplate(cleanedName, ids));
            StatusMessage = "Šablona uložena.";
            SetEditor(null);
            IsBusy = false;
            await LoadAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se uložit šablonu.");
            StatusMessage = exception is WorkoutTemplateException ? exception.Message : "Šablonu se nepodařilo uložit.";
        }
        finally { IsBusy = false; }
    }

    private async Task DeleteAsync(WorkoutTemplateItem? item)
    {
        if (IsBusy || item is null) return;
        IsBusy = true;
        try { await repository.DeleteAsync(item.Id); StatusMessage = "Šablona smazána."; IsBusy = false; await LoadAsync(); }
        catch (Exception exception) { logger.LogError(exception, "Nepodařilo se smazat šablonu."); StatusMessage = "Šablonu se nepodařilo smazat."; }
        finally { IsBusy = false; }
    }

    private void SetEditor(WorkoutTemplateItem? item)
    {
        editingId = item?.Id;
        isEditorOpen = item is not null;
        Name = item?.Name ?? string.Empty;
        PlannedExercises.Clear();
        if (item is not null)
            foreach (var planned in item.Template.Exercises)
                if (AvailableExercises.FirstOrDefault(exercise => exercise.Id == planned.ExerciseId) is { } exercise)
                    PlannedExercises.Add(new(exercise));
        NotifyEditor();
    }

    private void NotifyEditor() { OnPropertyChanged(nameof(IsEditing)); UpdateCommands(); }
    private void UpdateCommands() { foreach (var command in commands) command.ChangeCanExecute(); }
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(propertyName); return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
