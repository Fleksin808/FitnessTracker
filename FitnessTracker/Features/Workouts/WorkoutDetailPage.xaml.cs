using Microsoft.Extensions.Logging;

namespace FitnessTracker.Features.Workouts;

public partial class WorkoutDetailPage : ContentPage, IQueryAttributable
{
    public const string Route = nameof(WorkoutDetailPage);
    public const string WorkoutIdParameter = "workoutId";
    private readonly WorkoutDetailViewModel viewModel;
    private readonly ILogger<WorkoutDetailPage> logger;
    public WorkoutDetailViewModel ViewModel => viewModel;
    private Guid workoutId;
    private bool isDeleteFlowActive;

    public WorkoutDetailPage(WorkoutDetailViewModel viewModel, ILogger<WorkoutDetailPage> logger)
    {
        this.viewModel = viewModel;
        this.logger = logger;
        InitializeComponent();
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // Shell předá navigační parametr; neplatná hodnota se později zobrazí jako chyba detailu.
        workoutId = query.TryGetValue(WorkoutIdParameter, out var value)
            && Guid.TryParse(value?.ToString(), out var parsedId)
            ? parsedId
            : Guid.Empty;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Příjem parametru je synchronní; skutečné načtení počkáme v životním cyklu stránky.
        // ViewModel ošetřuje chybu úložiště i neexistující nebo prázdné ID.
        await viewModel.LoadAsync(workoutId);
    }

    private async void OnDeleteWorkoutClicked(object? sender, EventArgs e)
    {
        if (isDeleteFlowActive || viewModel.Workout is not { } workout) return;
        isDeleteFlowActive = true;
        bool workoutDeleted = false;
        try
        {
            // Potvrzení jasně říká, že se smaže celý trénink včetně cviků a sérií.
            bool confirmed = await DisplayAlertAsync("Smazat trénink?",
                $"Opravdu chceš smazat „{workout.Name}“ včetně všech zaznamenaných cviků a sérií? Katalog cviků zůstane zachován.",
                "Smazat", "Zrušit");
            if (!confirmed || !await viewModel.DeleteWorkoutAsync()) return;
            workoutDeleted = true;

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Nepodařilo se dokončit smazání tréninku.");
            string message = workoutDeleted
                ? "Trénink byl smazán, ale návrat se nezdařil. Vrať se do seznamu."
                : "Trénink se nepodařilo smazat ani otevřít seznam.";
            await DisplayAlertAsync("Smazání tréninku", message, "OK");
        }
        finally { isDeleteFlowActive = false; }
    }
}
