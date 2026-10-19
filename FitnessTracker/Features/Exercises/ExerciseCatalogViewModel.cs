using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FitnessTracker.Application.Exercises;
using FitnessTracker.Domain.Exercises;
using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.Exercises;

public sealed class ExerciseCatalogViewModel : INotifyPropertyChanged
{
    private readonly IExerciseRepository repository;
    private readonly ILogger<ExerciseCatalogViewModel> logger;
    private readonly Command[] commands;
    private Exercise? editingExercise;
    private string? name = string.Empty;
    private string? statusMessage;
    private string? loadError;
    private bool isBusy;
    private bool includeArchived;

    public ExerciseCatalogViewModel(IExerciseRepository repository, ILogger<ExerciseCatalogViewModel> logger)
    {
        this.repository = repository;
        this.logger = logger;
        var save = new Command(async () => await SaveAsync(), () => !IsBusy);
        var load = new Command(async () => await LoadAsync(), () => !IsBusy);
        var cancel = new Command(CancelEdit, () => !IsBusy && IsEditing);
        var edit = new Command<Exercise>(BeginEdit, _ => !IsBusy);
        var archive = new Command<Exercise>(async exercise => await ToggleArchiveAsync(exercise), _ => !IsBusy);
        var filter = new Command(async () => await ToggleArchivedVisibilityAsync(), () => !IsBusy);
        commands = [save, load, cancel, edit, archive, filter];
        SaveCommand = save;
        LoadCommand = load;
        CancelEditCommand = cancel;
        EditExerciseCommand = edit;
        ToggleArchiveCommand = archive;
        ToggleArchivedVisibilityCommand = filter;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<Exercise> Exercises { get; } = new();
    public ICommand SaveCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand EditExerciseCommand { get; }
    public ICommand ToggleArchiveCommand { get; }
    public ICommand ToggleArchivedVisibilityCommand { get; }

    public string? Name { get => name; set => SetField(ref name, value); }
    public string? StatusMessage { get => statusMessage; private set => SetField(ref statusMessage, value); }
    public string? LoadError { get => loadError; private set => SetField(ref loadError, value); }
    public bool IsEditing => editingExercise is not null;
    public bool CanInteract => !IsBusy;
    public string EditorTitle => IsEditing ? "Upravit cvik" : "Nový cvik";
    public string SaveButtonText => IsEditing ? "Uložit změnu" : "Přidat cvik";
    public bool IncludeArchived => includeArchived;
    public string ArchiveFilterText => IncludeArchived ? "Skrýt archivované" : "Zobrazit i archivované";

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (!SetField(ref isBusy, value))
                return;
            OnPropertyChanged(nameof(CanInteract));
            UpdateCommands();
        }
    }

    public async Task LoadAsync()
    {
        if (IsBusy)
            return;
        IsBusy = true;
        try { await RefreshAsync(); }
        finally { IsBusy = false; }
    }

    public void BeginEdit(Exercise? exercise)
    {
        if (IsBusy || exercise is null)
            return;
        SetEditor(exercise);
        StatusMessage = null;
    }

    public void CancelEdit()
    {
        if (!IsBusy)
            SetEditor(null);
    }

    public async Task SaveAsync()
    {
        if (IsBusy)
            return;
        string? enteredName = Name?.Trim();
        if (string.IsNullOrWhiteSpace(enteredName))
        {
            StatusMessage = "Nejdřív zadej název cviku.";
            return;
        }

        // Zapamatujeme upravovaný objekt. Přejmenování zachová ID i stav archivace.
        var original = editingExercise;
        await ExecuteWriteAsync(() => original is null
            ? repository.AddAsync(new Exercise(enteredName))
            : repository.UpdateAsync(original.WithName(enteredName)), "Cvik uložen.", resetEditor: true);
    }

    public async Task ToggleArchiveAsync(Exercise? exercise)
    {
        if (IsBusy || exercise is null)
            return;
        await ExecuteWriteAsync(
            () => repository.UpdateAsync(exercise.WithArchived(!exercise.IsArchived)),
            exercise.IsArchived ? "Cvik obnoven do aktivního katalogu." : "Cvik archivován.",
            resetEditor: editingExercise?.Id == exercise.Id);
    }

    public async Task ToggleArchivedVisibilityAsync()
    {
        if (IsBusy)
            return;
        IsBusy = true;
        bool previous = IncludeArchived;
        try
        {
            SetArchiveVisibility(!previous);
            // Při chybě ponecháme původní seznam i jeho filtr, aby si vzájemně odpovídaly.
            if (!await RefreshAsync())
                SetArchiveVisibility(previous);
        }
        finally { IsBusy = false; }
    }

    private async Task ExecuteWriteAsync(Func<Task> operation, string successMessage, bool resetEditor)
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            await operation();
            if (resetEditor)
                SetEditor(null);
            // Chyba obnovy seznamu nesmí tvrdit, že se zápis nepodařil, a vést k duplicitě.
            bool refreshed = await RefreshAsync();
            StatusMessage = refreshed ? successMessage
                : successMessage + " Seznam se nepodařilo obnovit. Použij Obnovit seznam.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se uložit změnu v katalogu cviků.");
            StatusMessage = exception is KeyNotFoundException
                ? "Cvik už není dostupný. Obnov seznam a vyber ho znovu."
                : "Změnu se nepodařilo uložit. Zkus to znovu.";
        }
        finally { IsBusy = false; }
    }

    private async Task<bool> RefreshAsync()
    {
        LoadError = null;
        try
        {
            var loaded = await repository.GetAllAsync(IncludeArchived);
            // Při chybě čtení necháme původní položky. Kolekci měníme v kontextu UI.
            Exercises.Clear();
            foreach (var exercise in loaded)
                Exercises.Add(exercise);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se načíst katalog cviků.");
            LoadError = "Katalog se nepodařilo načíst. Zkus obnovit seznam.";
            return false;
        }
    }

    private void SetEditor(Exercise? exercise)
    {
        editingExercise = exercise;
        Name = exercise?.Name ?? string.Empty;
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(SaveButtonText));
        UpdateCommands();
    }

    private void SetArchiveVisibility(bool value)
    {
        includeArchived = value;
        OnPropertyChanged(nameof(IncludeArchived));
        OnPropertyChanged(nameof(ArchiveFilterText));
    }

    private void UpdateCommands()
    {
        foreach (var command in commands)
            command.ChangeCanExecute();
    }

    // Společný setter pro vlastnosti tohoto ViewModelu omezuje opakování oznamovacího kódu.
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
